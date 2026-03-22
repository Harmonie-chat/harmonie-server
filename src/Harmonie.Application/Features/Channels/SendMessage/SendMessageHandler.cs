using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed record SendChannelMessageInput(GuildChannelId ChannelId, SendMessageRequest Request);

public sealed class SendMessageHandler : IAuthenticatedHandler<SendChannelMessageInput, SendMessageResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _channelMessageRepository;
    private readonly MessageAttachmentResolver _messageAttachmentResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository channelMessageRepository,
        MessageAttachmentResolver messageAttachmentResolver,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        ILogger<SendMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _messageAttachmentResolver = messageAttachmentResolver;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        SendChannelMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var contentResult = MessageContent.Create(request.Request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            var code = MessageContentErrorCodeResolver.Resolve(request.Request.Content);
            return ApplicationResponse<SendMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
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
            request.Request.AttachmentFileIds,
            currentUserId,
            cancellationToken);
        if (!attachmentResolution.Success)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.Request.AttachmentFileIds),
                    ApplicationErrorCodes.Validation.Invalid,
                    attachmentResolution.Error ?? "Attachments are invalid"));
        }

        var messageResult = Message.CreateForChannel(
            request.ChannelId,
            currentUserId,
            contentResult.Value,
            attachmentResolution.Attachments);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
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
                messageResult.Value.Content.Value,
                messageResult.Value.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                messageResult.Value.CreatedAtUtc));

        var payload = new SendMessageResponse(
            MessageId: messageResult.Value.Id.ToString(),
            ChannelId: messageChannelId.ToString(),
            AuthorUserId: messageResult.Value.AuthorUserId.ToString(),
            Content: messageResult.Value.Content.Value,
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
