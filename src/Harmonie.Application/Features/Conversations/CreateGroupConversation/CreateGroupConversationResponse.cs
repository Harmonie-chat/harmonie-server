namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public sealed record CreateGroupConversationResponse(
    Guid ConversationId,
    string Type,
    string? Name,
    IReadOnlyList<Guid> ParticipantIds,
    DateTime CreatedAtUtc);
