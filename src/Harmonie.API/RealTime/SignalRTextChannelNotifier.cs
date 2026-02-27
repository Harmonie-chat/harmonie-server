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
}

public sealed record MessageCreatedEvent(
    string MessageId,
    string ChannelId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
