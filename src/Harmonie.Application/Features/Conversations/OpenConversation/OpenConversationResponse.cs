namespace Harmonie.Application.Features.Conversations.OpenConversation;

public sealed record OpenConversationResponse(
    Guid ConversationId,
    string Type,
    IReadOnlyList<Guid> ParticipantIds,
    DateTime CreatedAtUtc,
    bool Created);
