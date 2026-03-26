namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public sealed record CreateGroupConversationResponse(
    string ConversationId,
    string Type,
    string? Name,
    IReadOnlyList<string> ParticipantIds,
    DateTime CreatedAtUtc);
