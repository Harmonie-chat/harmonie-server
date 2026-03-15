using Harmonie.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

public sealed class SignalRReactionNotifier : IReactionNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRReactionNotifier(IHubContext<RealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyReactionAddedToChannelAsync(
        ChannelReactionAddedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionAddedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: notification.ChannelId.ToString(),
            GuildId: notification.GuildId.ToString(),
            ConversationId: null,
            UserId: notification.UserId.ToString(),
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("ReactionAdded", payload, cancellationToken);
    }

    public async Task NotifyReactionAddedToConversationAsync(
        ConversationReactionAddedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionAddedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: null,
            GuildId: null,
            ConversationId: notification.ConversationId.ToString(),
            UserId: notification.UserId.ToString(),
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .SendAsync("ReactionAdded", payload, cancellationToken);
    }

    public async Task NotifyReactionRemovedFromChannelAsync(
        ChannelReactionRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionRemovedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: notification.ChannelId.ToString(),
            GuildId: notification.GuildId.ToString(),
            ConversationId: null,
            UserId: notification.UserId.ToString(),
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("ReactionRemoved", payload, cancellationToken);
    }

    public async Task NotifyReactionRemovedFromConversationAsync(
        ConversationReactionRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ReactionRemovedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: null,
            GuildId: null,
            ConversationId: notification.ConversationId.ToString(),
            UserId: notification.UserId.ToString(),
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .SendAsync("ReactionRemoved", payload, cancellationToken);
    }
}

public sealed record ReactionAddedEvent(
    string MessageId,
    string? ChannelId,
    string? GuildId,
    string? ConversationId,
    string UserId,
    string Emoji);

public sealed record ReactionRemovedEvent(
    string MessageId,
    string? ChannelId,
    string? GuildId,
    string? ConversationId,
    string UserId,
    string Emoji);
