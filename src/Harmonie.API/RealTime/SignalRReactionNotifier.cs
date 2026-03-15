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
            ConversationId: notification.ConversationId.ToString(),
            UserId: notification.UserId.ToString(),
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .SendAsync("ReactionAdded", payload, cancellationToken);
    }
}

public sealed record ReactionAddedEvent(
    string MessageId,
    string? ChannelId,
    string? ConversationId,
    string UserId,
    string Emoji);
