using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IConversationReadStateRepository
{
    Task UpsertAsync(
        UserId userId,
        ConversationId conversationId,
        MessageId lastReadMessageId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);
}
