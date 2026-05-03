using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Messages;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Messages;

public sealed class SignalRPinNotifier : IPinNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRPinNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMessagePinnedInChannelAsync(
        ChannelPinAddedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessagePinnedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value,
            ConversationId: null,
            PinnedByUserId: notification.PinnedByUserId.Value,
            PinnedByUsername: notification.PinnedByUsername,
            PinnedByDisplayName: notification.PinnedByDisplayName,
            PinnedAtUtc: notification.PinnedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessagePinned(payload, cancellationToken);
    }

    public async Task NotifyMessagePinnedInConversationAsync(
        ConversationPinAddedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessagePinnedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: null,
            GuildId: null,
            ConversationId: notification.ConversationId.Value,
            PinnedByUserId: notification.PinnedByUserId.Value,
            PinnedByUsername: notification.PinnedByUsername,
            PinnedByDisplayName: notification.PinnedByDisplayName,
            PinnedAtUtc: notification.PinnedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .MessagePinned(payload, cancellationToken);
    }

    public async Task NotifyMessageUnpinnedInChannelAsync(
        ChannelPinRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageUnpinnedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value,
            ConversationId: null,
            UnpinnedByUserId: notification.UnpinnedByUserId.Value,
            UnpinnedByUsername: notification.UnpinnedByUsername,
            UnpinnedByDisplayName: notification.UnpinnedByDisplayName,
            UnpinnedAtUtc: notification.UnpinnedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessageUnpinned(payload, cancellationToken);
    }

    public async Task NotifyMessageUnpinnedInConversationAsync(
        ConversationPinRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageUnpinnedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: null,
            GuildId: null,
            ConversationId: notification.ConversationId.Value,
            UnpinnedByUserId: notification.UnpinnedByUserId.Value,
            UnpinnedByUsername: notification.UnpinnedByUsername,
            UnpinnedByDisplayName: notification.UnpinnedByDisplayName,
            UnpinnedAtUtc: notification.UnpinnedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .MessageUnpinned(payload, cancellationToken);
    }
}

public sealed record MessagePinnedEvent(
    Guid MessageId,
    Guid? ChannelId,
    Guid? GuildId,
    Guid? ConversationId,
    Guid PinnedByUserId,
    string PinnedByUsername,
    string? PinnedByDisplayName,
    DateTime PinnedAtUtc);

public sealed record MessageUnpinnedEvent(
    Guid MessageId,
    Guid? ChannelId,
    Guid? GuildId,
    Guid? ConversationId,
    Guid UnpinnedByUserId,
    string UnpinnedByUsername,
    string? UnpinnedByDisplayName,
    DateTime UnpinnedAtUtc);
