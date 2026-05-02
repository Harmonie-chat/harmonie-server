using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public sealed record ConversationParticipantSummary(
    UserId UserId,
    Username Username,
    string? DisplayName,
    Guid? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg);

public sealed record ConversationGetOrCreateResult(Conversation Conversation, bool WasCreated);

public sealed record ConversationAccess(
    Conversation Conversation,
    ConversationParticipant? Participant,
    string? CallerUsername = null,
    string? CallerDisplayName = null);

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

    Task<ConversationAccess?> GetByIdWithParticipantCheckAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);
}
