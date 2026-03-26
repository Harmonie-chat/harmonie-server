namespace Harmonie.Application.Features.Conversations.ListConversations;

public sealed record ListConversationsResponse(
    IReadOnlyList<ListConversationsItemResponse> Conversations);

public sealed record ListConversationsItemResponse(
    string ConversationId,
    string Type,
    string? Name,
    IReadOnlyList<ListConversationsParticipantDto> Participants,
    DateTime CreatedAtUtc);

public sealed record ListConversationsParticipantDto(string UserId, string Username);
