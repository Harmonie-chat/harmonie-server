using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.RemoveReaction;

public sealed class RemoveReactionHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageReactionRepository _reactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReactionNotifier _reactionNotifier;
    private readonly ILogger<RemoveReactionHandler> _logger;

    public RemoveReactionHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IMessageReactionRepository reactionRepository,
        IUnitOfWork unitOfWork,
        IReactionNotifier reactionNotifier,
        ILogger<RemoveReactionHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _reactionRepository = reactionRepository;
        _unitOfWork = unitOfWork;
        _reactionNotifier = reactionNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildChannelId channelId,
        MessageId messageId,
        string emoji,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "RemoveChannelReaction started. ChannelId={ChannelId}, MessageId={MessageId}, Emoji={Emoji}, CallerId={CallerId}",
            channelId,
            messageId,
            emoji,
            callerId);

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "RemoveChannelReaction failed because channel was not found. ChannelId={ChannelId}",
                channelId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "RemoveChannelReaction failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}",
                channelId,
                ctx.Channel.Type);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Reactions can only be removed from text channels");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "RemoveChannelReaction access denied because caller is not a member. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}",
                channelId,
                ctx.Channel.GuildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);
        var messageChannelId = message?.ChannelId;
        if (message is null || messageChannelId is null || messageChannelId != channelId)
        {
            _logger.LogWarning(
                "RemoveChannelReaction failed because message was not found. ChannelId={ChannelId}, MessageId={MessageId}",
                channelId,
                messageId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _reactionRepository.RemoveAsync(messageId, callerId, emoji, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "RemoveChannelReaction succeeded. ChannelId={ChannelId}, MessageId={MessageId}, Emoji={Emoji}, CallerId={CallerId}",
            channelId,
            messageId,
            emoji,
            callerId);

        await NotifyReactionRemovedSafelyAsync(
            new ChannelReactionRemovedNotification(messageId, channelId, callerId, emoji));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyReactionRemovedSafelyAsync(
        ChannelReactionRemovedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _reactionNotifier.NotifyReactionRemovedFromChannelAsync(notification, token),
            NotificationTimeout,
            _logger,
            "RemoveChannelReaction notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
