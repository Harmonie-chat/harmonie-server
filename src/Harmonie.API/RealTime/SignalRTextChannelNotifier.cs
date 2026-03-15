using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

public sealed class SignalRTextChannelNotifier : ITextChannelNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRTextChannelNotifier(IHubContext<RealtimeHub> hubContext)
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
            GuildId: notification.GuildId.ToString(),
            AuthorUserId: notification.AuthorUserId.ToString(),
            Content: notification.Content,
            Attachments: notification.Attachments,
            CreatedAtUtc: notification.CreatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
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
            GuildId: notification.GuildId.ToString(),
            Content: notification.Content,
            UpdatedAtUtc: notification.UpdatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("MessageUpdated", payload, cancellationToken);
    }

    public async Task NotifyMessageDeletedAsync(
        TextChannelMessageDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageDeletedEvent(
            MessageId: notification.MessageId.ToString(),
            ChannelId: notification.ChannelId.ToString(),
            GuildId: notification.GuildId.ToString());

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .SendAsync("MessageDeleted", payload, cancellationToken);
    }
}

public sealed record MessageCreatedEvent(
    string MessageId,
    string ChannelId,
    string GuildId,
    string AuthorUserId,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public sealed record MessageUpdatedEvent(
    string MessageId,
    string ChannelId,
    string GuildId,
    string Content,
    DateTime UpdatedAtUtc);

public sealed record MessageDeletedEvent(
    string MessageId,
    string ChannelId,
    string GuildId);
