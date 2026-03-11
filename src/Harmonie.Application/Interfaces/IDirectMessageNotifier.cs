using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IDirectMessageNotifier
{
    Task NotifyMessageCreatedAsync(
        DirectMessageCreatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageUpdatedAsync(
        DirectMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageDeletedAsync(
        DirectMessageDeletedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record DirectMessageCreatedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    UserId AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);

public sealed record DirectMessageUpdatedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string Content,
    DateTime UpdatedAtUtc);

public sealed record DirectMessageDeletedNotification(
    MessageId MessageId,
    ConversationId ConversationId);
