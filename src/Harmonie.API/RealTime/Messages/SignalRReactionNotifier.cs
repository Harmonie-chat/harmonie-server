using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Messages;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Messages;

public sealed class SignalRReactionNotifier : IReactionNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRReactionNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyReactionAddedToChannelAsync(
        ChannelReactionAddedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionAddedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value,
            ConversationId: null,
            UserId: notification.UserId.Value,
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .ReactionAdded(payload, cancellationToken);
    }

    public async Task NotifyReactionAddedToConversationAsync(
        ConversationReactionAddedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionAddedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: null,
            GuildId: null,
            ConversationId: notification.ConversationId.Value,
            UserId: notification.UserId.Value,
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ReactionAdded(payload, cancellationToken);
    }

    public async Task NotifyReactionRemovedFromChannelAsync(
        ChannelReactionRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionRemovedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value,
            ConversationId: null,
            UserId: notification.UserId.Value,
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .ReactionRemoved(payload, cancellationToken);
    }

    public async Task NotifyReactionRemovedFromConversationAsync(
        ConversationReactionRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionRemovedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: null,
            GuildId: null,
            ConversationId: notification.ConversationId.Value,
            UserId: notification.UserId.Value,
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ReactionRemoved(payload, cancellationToken);
    }
}

public sealed record ReactionAddedEvent(
    Guid MessageId,
    Guid? ChannelId,
    Guid? GuildId,
    Guid? ConversationId,
    Guid UserId,
    string Emoji);

public sealed record ReactionRemovedEvent(
    Guid MessageId,
    Guid? ChannelId,
    Guid? GuildId,
    Guid? ConversationId,
    Guid UserId,
    string Emoji);
