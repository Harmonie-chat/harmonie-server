using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public interface IConversationParticipantRepository
{
    Task<bool> TryAddAsync(
        ConversationParticipant participant,
        CancellationToken cancellationToken = default);

    Task<ConversationParticipant?> GetAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ConversationParticipant participant,
        CancellationToken cancellationToken = default);

    Task<int> RemoveAsync(
        ConversationParticipant participant,
        CancellationToken cancellationToken = default);
}
