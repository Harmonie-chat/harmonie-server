using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public sealed record ConversationParticipantSummary(UserId UserId, Username Username);

public sealed record ConversationGetOrCreateResult(Conversation Conversation, bool WasCreated);

public sealed record UserConversationSummary(
    ConversationId ConversationId,
    ConversationType Type,
    string? Name,
    IReadOnlyList<ConversationParticipantSummary> Participants,
    DateTime CreatedAtUtc);

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);

    Task<ConversationGetOrCreateResult> GetOrCreateDirectAsync(
        UserId firstUserId,
        UserId secondUserId,
        CancellationToken cancellationToken = default);

    Task<Conversation> CreateGroupAsync(
        string? name,
        IReadOnlyList<UserId> participantIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserConversationSummary>> GetUserConversationsAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsParticipantAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default);
}
