using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IDirectMessageNotifier
{
    Task NotifyMessageCreatedAsync(
        DirectMessageCreatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record DirectMessageCreatedNotification(
    DirectMessageId MessageId,
    ConversationId ConversationId,
    UserId AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
