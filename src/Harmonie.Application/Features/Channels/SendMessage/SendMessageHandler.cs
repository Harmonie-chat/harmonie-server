using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageHandler
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
        GuildChannelId channelId,
        SendMessageRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SendMessage started. ChannelId={ChannelId}, UserId={UserId}",
            channelId,
            currentUserId);

        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            _logger.LogWarning(
                "SendMessage validation failed. ChannelId={ChannelId}, UserId={UserId}, Error={Error}",
                channelId,
                currentUserId,
                contentResult.Error);

            var code = MessageContentErrorCodeResolver.Resolve(request.Content);
            return ApplicationResponse<SendMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "SendMessage failed because channel was not found. ChannelId={ChannelId}, UserId={UserId}",
                channelId,
                currentUserId);

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "SendMessage failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}, UserId={UserId}",
                channelId,
                ctx.Channel.Type,
                currentUserId);

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be sent to text channels");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "SendMessage access denied. ChannelId={ChannelId}, GuildId={GuildId}, UserId={UserId}",
                channelId,
                ctx.Channel.GuildId,
                currentUserId);

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
            channelId,
            currentUserId,
            contentResult.Value,
            attachmentResolution.Attachments);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            _logger.LogWarning(
                "SendMessage domain creation failed. ChannelId={ChannelId}, UserId={UserId}, Error={Error}",
                channelId,
                currentUserId,
                messageResult.Error);

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

        _logger.LogInformation(
            "SendMessage succeeded. MessageId={MessageId}, ChannelId={ChannelId}, UserId={UserId}",
            messageResult.Value.Id,
            messageChannelId,
            messageResult.Value.AuthorUserId);

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
