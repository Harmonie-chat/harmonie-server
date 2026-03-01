using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.DeleteMessage;

public sealed class DeleteMessageHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<DeleteMessageHandler> _logger;

    public DeleteMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IChannelMessageRepository channelMessageRepository,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        ILogger<DeleteMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _channelMessageRepository = channelMessageRepository;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildChannelId channelId,
        ChannelMessageId messageId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteMessage started. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
            channelId,
            messageId,
            callerId);

        var channel = await _guildChannelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel is null)
        {
            _logger.LogWarning(
                "DeleteMessage failed because channel was not found. ChannelId={ChannelId}",
                channelId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "DeleteMessage failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}",
                channelId,
                channel.Type);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be deleted in text channels");
        }

        var role = await _guildMemberRepository.GetRoleAsync(channel.GuildId, callerId, cancellationToken);
        if (role is null)
        {
            _logger.LogWarning(
                "DeleteMessage failed because caller is not a member. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}",
                channelId,
                channel.GuildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _channelMessageRepository.GetByIdAsync(messageId, cancellationToken);
        if (message is null || message.ChannelId != channelId)
        {
            _logger.LogWarning(
                "DeleteMessage failed because message was not found. ChannelId={ChannelId}, MessageId={MessageId}",
                channelId,
                messageId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != callerId && role != GuildRole.Admin)
        {
            _logger.LogWarning(
                "DeleteMessage forbidden because caller is not the author or an admin. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
                channelId,
                messageId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete your own messages unless you are a guild admin");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelMessageRepository.DeleteAsync(messageId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "DeleteMessage succeeded. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
            channelId,
            messageId,
            callerId);

        await NotifyMessageDeletedSafelyAsync(
            new TextChannelMessageDeletedNotification(messageId, channelId));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyMessageDeletedSafelyAsync(
        TextChannelMessageDeletedNotification notification)
    {
        try
        {
            using var notificationCts = new CancellationTokenSource(NotificationTimeout);
            await _textChannelNotifier.NotifyMessageDeletedAsync(notification, notificationCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "DeleteMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
                notification.MessageId,
                notification.ChannelId);
        }
    }
}
