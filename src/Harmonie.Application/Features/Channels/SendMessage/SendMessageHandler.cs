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

public sealed record SendChannelMessageInput(GuildChannelId ChannelId, string? Content, IReadOnlyList<Guid>? AttachmentFileIds = null);

public sealed class SendMessageHandler : IAuthenticatedHandler<SendChannelMessageInput, SendMessageResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _channelMessageRepository;
    private readonly MessageAttachmentResolver _messageAttachmentResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository channelMessageRepository,
        MessageAttachmentResolver messageAttachmentResolver,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        LinkPreviewResolutionService linkPreviewService,
        ILogger<SendMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _messageAttachmentResolver = messageAttachmentResolver;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _linkPreviewService = linkPreviewService;
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

        var messageResult = Message.CreateForChannel(
            request.ChannelId,
            currentUserId,
            content,
            attachmentResolution.Attachments);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            var errorCode = content is null && attachmentResolution.Attachments.Count == 0
                ? ApplicationErrorCodes.Message.ContentEmpty
                : ApplicationErrorCodes.Common.DomainRuleViolation;
            return ApplicationResponse<SendMessageResponse>.Fail(
                errorCode,
                messageResult.Error ?? "Unable to create channel message");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelMessageRepository.AddAsync(messageResult.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var messageChannelId = messageResult.Value.ChannelId;
        if (messageChannelId is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Channel message creation succeeded but channel ID is missing");
        }

        await NotifyMessageCreatedSafelyAsync(
            new TextChannelMessageCreatedNotification(
                messageResult.Value.Id,
                messageChannelId,
                ctx.Channel.GuildId,
                messageResult.Value.AuthorUserId,
                ctx.CallerUsername ?? string.Empty,
                ctx.CallerDisplayName,
                messageResult.Value.Content?.Value,
                messageResult.Value.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                messageResult.Value.CreatedAtUtc));

        var urls = _linkPreviewService.ParseUrls(messageResult.Value.Content?.Value);
        if (urls.Count > 0)
        {
            _ = _linkPreviewService.ResolveAndNotifyForChannelAsync(
                messageResult.Value.Id,
                messageChannelId,
                ctx.Channel.GuildId,
                urls,
                cancellationToken);
        }

        var payload = new SendMessageResponse(
            MessageId: messageResult.Value.Id.Value,
            ChannelId: messageChannelId.Value,
            AuthorUserId: messageResult.Value.AuthorUserId.Value,
            Content: messageResult.Value.Content?.Value,
            Attachments: messageResult.Value.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
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
