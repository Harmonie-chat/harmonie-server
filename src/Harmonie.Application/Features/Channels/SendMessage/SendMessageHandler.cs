using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Services;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed record SendChannelMessageInput(GuildChannelId ChannelId, string? Content, IReadOnlyList<Guid>? AttachmentFileIds = null, Guid? ReplyToMessageId = null);

public sealed class SendMessageHandler : IAuthenticatedHandler<SendChannelMessageInput, SendMessageResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _channelMessageRepository;
    private readonly IMessageAttachmentRepository _messageAttachmentRepository;
    private readonly MessageAttachmentResolver _messageAttachmentResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository channelMessageRepository,
        IMessageAttachmentRepository messageAttachmentRepository,
        MessageAttachmentResolver messageAttachmentResolver,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        LinkPreviewResolutionService linkPreviewService,
        IMessageRepository messageRepository,
        ILogger<SendMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _messageAttachmentResolver = messageAttachmentResolver;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _linkPreviewService = linkPreviewService;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        SendChannelMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        MessageContent? content = null;
        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            var contentResult = MessageContent.Create(request.Content);
            if (contentResult.IsFailure || contentResult.Value is null)
            {
                var code = MessageContentErrorCodeResolver.Resolve(request.Content);
                return ApplicationResponse<SendMessageResponse>.Fail(
                    code,
                    contentResult.Error ?? "Message content is invalid");
            }
            content = contentResult.Value;
        }

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be sent to text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        // Resolve and validate reply target
        MessageId? replyToMessageId = null;
        ReplyTargetSummary? replyTargetSummary = null;
        if (request.ReplyToMessageId.HasValue)
        {
            var targetMessageId = MessageId.From(request.ReplyToMessageId.Value);
            replyTargetSummary = await _messageRepository.GetReplyTargetSummaryAsync(targetMessageId, cancellationToken);
            if (replyTargetSummary is null || !replyTargetSummary.Scope.Matches(request.ChannelId))
            {
                return ApplicationResponse<SendMessageResponse>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Reply target message was not found");
            }
            replyToMessageId = replyTargetSummary.MessageId;
        }

        var attachmentResolution = await _messageAttachmentResolver.ResolveAsync(
            request.AttachmentFileIds,
            currentUserId,
            cancellationToken);
        if (!attachmentResolution.Success)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.AttachmentFileIds),
                    ApplicationErrorCodes.Validation.Invalid,
                    attachmentResolution.Error ?? "Attachments are invalid"));
        }

        if (content is null && attachmentResolution.Attachments.Count == 0)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Message.ContentEmpty,
                "Message must have content or at least one attachment");
        }

        var messageResult = Message.Create(
            new MessageScope.Channel(request.ChannelId),
            currentUserId,
            content,
            replyToMessageId);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                messageResult.Error ?? "Unable to create channel message");
        }

        var attachments = new List<MessageAttachment>(attachmentResolution.Attachments.Count);
        for (var i = 0; i < attachmentResolution.Attachments.Count; i++)
        {
            var resolved = attachmentResolution.Attachments[i];
            var attachmentResult = MessageAttachment.Create(
                messageResult.Value.Id,
                resolved.FileId,
                resolved.FileName,
                resolved.ContentType,
                resolved.SizeBytes,
                position: i);
            if (attachmentResult.IsFailure || attachmentResult.Value is null)
            {
                return ApplicationResponse<SendMessageResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    attachmentResult.Error ?? "Unable to create message attachment");
            }
            attachments.Add(attachmentResult.Value);
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelMessageRepository.AddAsync(messageResult.Value, cancellationToken);
        if (attachments.Count > 0)
            await _messageAttachmentRepository.AddRangeAsync(attachments, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var messageChannelId = request.ChannelId;

        ReplyPreviewDto? replyTo = null;
        if (replyTargetSummary is not null)
        {
            replyTo = new ReplyPreviewDto(
                replyTargetSummary.MessageId.Value,
                replyTargetSummary.AuthorUserId.Value,
                replyTargetSummary.AuthorDisplayName,
                replyTargetSummary.AuthorUsername,
                replyTargetSummary.Content,
                replyTargetSummary.HasAttachments,
                replyTargetSummary.IsDeleted,
                replyTargetSummary.DeletedAtUtc);
        }

        await NotifyMessageCreatedSafelyAsync(
            new TextChannelMessageCreatedNotification(
                messageResult.Value.Id,
                messageChannelId,
                ctx.Channel.Name,
                ctx.Channel.GuildId,
                ctx.GuildName ?? string.Empty,
                messageResult.Value.AuthorUserId,
                ctx.CallerUsername ?? string.Empty,
                ctx.CallerDisplayName,
                messageResult.Value.Content?.Value,
                attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                replyTo,
                messageResult.Value.CreatedAtUtc));

        var urls = _linkPreviewService.ParseUrls(messageResult.Value.Content?.Value);
        if (urls.Count > 0)
        {
            // TODO: Replace fire-and-forget with a domain event + dedicated background worker
            // (e.g. MessageCreatedDomainEvent -> LinkPreviewResolutionWorker via a channel/queue).
            // This avoids scoped-service lifetime issues and gives proper retry/observability.
            _ = _linkPreviewService.ResolveAndNotifyForChannelAsync(
                messageResult.Value.Id,
                messageChannelId,
                ctx.Channel.Name,
                ctx.Channel.GuildId,
                ctx.GuildName ?? string.Empty,
                urls,
                cancellationToken);
        }

        var payload = new SendMessageResponse(
            MessageId: messageResult.Value.Id.Value,
            ChannelId: messageChannelId.Value,
            AuthorUserId: messageResult.Value.AuthorUserId.Value,
            Content: messageResult.Value.Content?.Value,
            Attachments: attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
            ReplyTo: replyTo,
            CreatedAtUtc: messageResult.Value.CreatedAtUtc);

        return ApplicationResponse<SendMessageResponse>.Ok(payload);
    }

    private async Task NotifyMessageCreatedSafelyAsync(
        TextChannelMessageCreatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _textChannelNotifier.NotifyMessageCreatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "SendMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
