using Harmonie.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

public sealed class SignalRDirectMessageNotifier : IDirectMessageNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRDirectMessageNotifier(IHubContext<RealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMessageCreatedAsync(
        DirectMessageCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new DirectMessageCreatedEvent(
            MessageId: notification.MessageId.ToString(),
            ConversationId: notification.ConversationId.ToString(),
            AuthorUserId: notification.AuthorUserId.ToString(),
            Content: notification.Content,
            CreatedAtUtc: notification.CreatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .SendAsync("DirectMessageCreated", payload, cancellationToken);
    }
}

public sealed record DirectMessageCreatedEvent(
    string MessageId,
    string ConversationId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
