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
            ChannelName: notification.ChannelName,
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            ConversationId: null,
            ConversationName: null,
            UserId: notification.UserId.Value,
            ReactorUsername: notification.Username,
            ReactorDisplayName: notification.DisplayName,
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
            ChannelName: null,
            GuildId: null,
            GuildName: null,
            ConversationId: notification.ConversationId.Value,
            ConversationName: null,
            UserId: notification.UserId.Value,
            ReactorUsername: notification.Username,
            ReactorDisplayName: notification.DisplayName,
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
            ChannelName: notification.ChannelName,
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            ConversationId: null,
            ConversationName: null,
            UserId: notification.UserId.Value,
            ReactorUsername: notification.Username,
            ReactorDisplayName: notification.DisplayName,
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
            ChannelName: null,
            GuildId: null,
            GuildName: null,
            ConversationId: notification.ConversationId.Value,
            ConversationName: null,
            UserId: notification.UserId.Value,
            ReactorUsername: notification.Username,
            ReactorDisplayName: notification.DisplayName,
            Emoji: notification.Emoji);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ReactionRemoved(payload, cancellationToken);
    }
}

public sealed record ReactionAddedEvent(
    Guid MessageId,
    Guid? ChannelId,
    string? ChannelName,
    Guid? GuildId,
    string? GuildName,
    Guid? ConversationId,
    string? ConversationName,
    Guid UserId,
    string ReactorUsername,
    string? ReactorDisplayName,
    string Emoji);

public sealed record ReactionRemovedEvent(
    Guid MessageId,
    Guid? ChannelId,
    string? ChannelName,
    Guid? GuildId,
    string? GuildName,
    Guid? ConversationId,
    string? ConversationName,
    Guid UserId,
    string ReactorUsername,
    string? ReactorDisplayName,
    string Emoji);
