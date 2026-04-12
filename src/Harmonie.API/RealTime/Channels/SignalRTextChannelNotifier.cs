using Harmonie.API.RealTime.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Channels;

public sealed class SignalRTextChannelNotifier : ITextChannelNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRTextChannelNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMessageCreatedAsync(
        TextChannelMessageCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageCreatedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value,
            AuthorUserId: notification.AuthorUserId.Value,
            Content: notification.Content,
            Attachments: notification.Attachments,
            CreatedAtUtc: notification.CreatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessageCreated(payload, cancellationToken);
    }

    public async Task NotifyMessageUpdatedAsync(
        TextChannelMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageUpdatedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value,
            Content: notification.Content,
            UpdatedAtUtc: notification.UpdatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessageUpdated(payload, cancellationToken);
    }

    public async Task NotifyMessageDeletedAsync(
        TextChannelMessageDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageDeletedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            GuildId: notification.GuildId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessageDeleted(payload, cancellationToken);
    }
}

public sealed record MessageCreatedEvent(
    Guid MessageId,
    Guid ChannelId,
    Guid GuildId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public sealed record MessageUpdatedEvent(
    Guid MessageId,
    Guid ChannelId,
    Guid GuildId,
    string? Content,
    DateTime UpdatedAtUtc);

public sealed record MessageDeletedEvent(
    Guid MessageId,
    Guid ChannelId,
    Guid GuildId);
