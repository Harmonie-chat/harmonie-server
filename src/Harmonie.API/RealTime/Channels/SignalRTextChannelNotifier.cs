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
            ChannelName: notification.ChannelName,
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            AuthorUserId: notification.AuthorUserId.Value,
            AuthorUsername: notification.AuthorUsername,
            AuthorDisplayName: notification.AuthorDisplayName,
            Content: notification.Content,
            Attachments: notification.Attachments,
            CreatedAtUtc: notification.CreatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessageCreated(payload, cancellationToken);
    }

    public async Task NotifyMessagePreviewUpdatedAsync(
        TextChannelMessagePreviewUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessagePreviewUpdatedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName,
            ConversationId: null,
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            Previews: notification.Previews);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessagePreviewUpdated(payload, cancellationToken);
    }

    public async Task NotifyMessageUpdatedAsync(
        TextChannelMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessageUpdatedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName,
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
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
            ChannelName: notification.ChannelName,
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName);

        await _hubContext.Clients
            .Group(RealtimeHub.GetChannelGroupName(notification.ChannelId))
            .MessageDeleted(payload, cancellationToken);
    }
}

public sealed record MessageCreatedEvent(
    Guid MessageId,
    Guid ChannelId,
    string ChannelName,
    Guid GuildId,
    string GuildName,
    Guid AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public sealed record MessageUpdatedEvent(
    Guid MessageId,
    Guid ChannelId,
    string ChannelName,
    Guid GuildId,
    string GuildName,
    string? Content,
    DateTime UpdatedAtUtc);

public sealed record MessageDeletedEvent(
    Guid MessageId,
    Guid ChannelId,
    string ChannelName,
    Guid GuildId,
    string GuildName);

public sealed record MessagePreviewUpdatedEvent(
    Guid MessageId,
    Guid? ChannelId,
    string? ChannelName,
    Guid? ConversationId,
    Guid? GuildId,
    string? GuildName,
    IReadOnlyList<LinkPreviewDto> Previews);
