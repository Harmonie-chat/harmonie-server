using Harmonie.API.RealTime.Channels;
using Harmonie.API.RealTime.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Conversations;

public sealed class SignalRConversationMessageNotifier : IConversationMessageNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRConversationMessageNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMessageCreatedAsync(
        ConversationMessageCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ConversationMessageCreatedEvent(
            MessageId: notification.MessageId.Value,
            ConversationId: notification.ConversationId.Value,
            AuthorUserId: notification.AuthorUserId.Value,
            AuthorUsername: notification.AuthorUsername,
            AuthorDisplayName: notification.AuthorDisplayName,
            Content: notification.Content,
            Attachments: notification.Attachments,
            CreatedAtUtc: notification.CreatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ConversationMessageCreated(payload, cancellationToken);
    }

    public async Task NotifyMessagePreviewUpdatedAsync(
        ConversationMessagePreviewUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MessagePreviewUpdatedEvent(
            MessageId: notification.MessageId.Value,
            ChannelId: null,
            ChannelName: null,
            ConversationId: notification.ConversationId.Value,
            GuildId: null,
            GuildName: null,
            Previews: notification.Previews);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .MessagePreviewUpdated(payload, cancellationToken);
    }

    public async Task NotifyMessageUpdatedAsync(
        ConversationMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ConversationMessageUpdatedEvent(
            MessageId: notification.MessageId.Value,
            ConversationId: notification.ConversationId.Value,
            Content: notification.Content,
            UpdatedAtUtc: notification.UpdatedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ConversationMessageUpdated(payload, cancellationToken);
    }

    public async Task NotifyMessageDeletedAsync(
        ConversationMessageDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ConversationMessageDeletedEvent(
            MessageId: notification.MessageId.Value,
            ConversationId: notification.ConversationId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetConversationGroupName(notification.ConversationId))
            .ConversationMessageDeleted(payload, cancellationToken);
    }
}

public sealed record ConversationMessageCreatedEvent(
    Guid MessageId,
    Guid ConversationId,
    Guid AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public sealed record ConversationMessageUpdatedEvent(
    Guid MessageId,
    Guid ConversationId,
    string? Content,
    DateTime UpdatedAtUtc);

public sealed record ConversationMessageDeletedEvent(
    Guid MessageId,
    Guid ConversationId);
