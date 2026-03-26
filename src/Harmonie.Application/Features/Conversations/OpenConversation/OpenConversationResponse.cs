namespace Harmonie.Application.Features.Conversations.OpenConversation;

public sealed record OpenConversationResponse(
    string ConversationId,
    string Type,
    IReadOnlyList<string> ParticipantIds,
    DateTime CreatedAtUtc,
    bool Created);
