using Harmonie.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

public sealed class SignalRTextChannelNotifier : ITextChannelNotifier
{
    private readonly IHubContext<TextChannelsHub> _hubContext;

    public SignalRTextChannelNotifier(IHubContext<TextChannelsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMessageCreatedAsync(
        TextChannelMessageCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageCreatedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: notification.ChannelId.ToString(),
            AuthorUserId: notification.AuthorUserId.ToString(),
            Content: notification.Content,
            CreatedAtUtc: notification.CreatedAtUtc);

        await _hubContext.Clients
            .Group(TextChannelsHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("MessageCreated", payload, cancellationToken);
    }

    public async Task NotifyMessageUpdatedAsync(
        TextChannelMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageUpdatedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: notification.ChannelId.ToString(),
            Content: notification.Content,
            UpdatedAtUtc: notification.UpdatedAtUtc);

        await _hubContext.Clients
            .Group(TextChannelsHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("MessageUpdated", payload, cancellationToken);
    }

    public async Task NotifyMessageDeletedAsync(
        TextChannelMessageDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageDeletedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: notification.ChannelId.ToString());

        await _hubContext.Clients
            .Group(TextChannelsHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("MessageDeleted", payload, cancellationToken);
    }
}

public sealed record MessageCreatedEvent(
    string MessageId,
    string ChannelId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);

public sealed record MessageUpdatedEvent(
    string MessageId,
    string ChannelId,
    string Content,
    DateTime UpdatedAtUtc);

public sealed record MessageDeletedEvent(
    string MessageId,
    string ChannelId);
