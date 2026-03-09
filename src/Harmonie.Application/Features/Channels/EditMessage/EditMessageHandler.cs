using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed class EditMessageHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<EditMessageHandler> _logger;

    public EditMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IChannelMessageRepository channelMessageRepository,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        ILogger<EditMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<EditMessageResponse>> HandleAsync(
        GuildChannelId channelId,
        ChannelMessageId messageId,
        EditMessageRequest request,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EditMessage started. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
            channelId,
            messageId,
            callerId);

        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            _logger.LogWarning(
                "EditMessage validation failed. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}, Error={Error}",
                channelId,
                messageId,
                callerId,
                contentResult.Error);

            var code = MessageContentErrorCodeResolver.Resolve(request.Content);
            return ApplicationResponse<EditMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "EditMessage failed because channel was not found. ChannelId={ChannelId}",
                channelId);

            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "EditMessage failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}",
                channelId,
                ctx.Channel.Type);

            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be edited in text channels");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "EditMessage access denied because caller is not a member. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}",
                channelId,
                ctx.Channel.GuildId,
                callerId);

            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _channelMessageRepository.GetByIdAsync(messageId, cancellationToken);
        if (message is null || message.ChannelId != channelId)
        {
            _logger.LogWarning(
                "EditMessage failed because message was not found. ChannelId={ChannelId}, MessageId={MessageId}",
                channelId,
                messageId);

            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != callerId)
        {
            _logger.LogWarning(
                "EditMessage forbidden because caller is not the author. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
                channelId,
                messageId,
                callerId);

            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Message.EditForbidden,
                "You can only edit your own messages");
        }

        var updateResult = message.UpdateContent(contentResult.Value);
        if (updateResult.IsFailure)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                updateResult.Error ?? "Message content update failed");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelMessageRepository.UpdateAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "EditMessage succeeded. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
            channelId,
            messageId,
            callerId);

        var updatedAtUtc = message.UpdatedAtUtc;
        if (updatedAtUtc is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Message edit succeeded but updated timestamp is missing");
        }

        await NotifyMessageUpdatedSafelyAsync(
            new TextChannelMessageUpdatedNotification(
                message.Id,
                message.ChannelId,
                message.Content.Value,
                updatedAtUtc.Value));

        return ApplicationResponse<EditMessageResponse>.Ok(new EditMessageResponse(
            MessageId: message.Id.ToString(),
            ChannelId: message.ChannelId.ToString(),
            AuthorUserId: message.AuthorUserId.ToString(),
            Content: message.Content.Value,
            CreatedAtUtc: message.CreatedAtUtc,
            UpdatedAtUtc: message.UpdatedAtUtc));
    }

    private async Task NotifyMessageUpdatedSafelyAsync(
        TextChannelMessageUpdatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _textChannelNotifier.NotifyMessageUpdatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "EditMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
