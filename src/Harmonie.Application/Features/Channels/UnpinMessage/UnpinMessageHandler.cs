using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.UnpinMessage;

public sealed record ChannelUnpinMessageInput(GuildChannelId ChannelId, MessageId MessageId);

public sealed class UnpinMessageHandler : IAuthenticatedHandler<ChannelUnpinMessageInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPinNotifier _pinNotifier;
    private readonly ILogger<UnpinMessageHandler> _logger;

    public UnpinMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        IUnitOfWork unitOfWork,
        IPinNotifier pinNotifier,
        ILogger<UnpinMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _unitOfWork = unitOfWork;
        _pinNotifier = pinNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ChannelUnpinMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be unpinned in text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageChannelId = message?.ChannelId;
        if (message is null || messageChannelId is null || messageChannelId != request.ChannelId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Pin.MessageNotFound,
                "Message was not found");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _pinnedMessageRepository.RemoveAsync(request.MessageId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyPinRemovedSafelyAsync(
            new ChannelPinRemovedNotification(
                request.MessageId,
                request.ChannelId,
                ctx.Channel.Name,
                ctx.Channel.GuildId,
                ctx.GuildName ?? string.Empty,
                currentUserId,
                ctx.CallerUsername ?? string.Empty,
                ctx.CallerDisplayName,
                DateTime.UtcNow));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyPinRemovedSafelyAsync(
        ChannelPinRemovedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _pinNotifier.NotifyMessageUnpinnedInChannelAsync(notification, token),
            NotificationTimeout,
            _logger,
            "Channel unpin notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
