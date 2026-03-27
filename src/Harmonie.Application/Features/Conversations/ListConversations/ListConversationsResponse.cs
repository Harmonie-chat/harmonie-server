namespace Harmonie.Application.Features.Conversations.ListConversations;

public sealed record ListConversationsResponse(
    IReadOnlyList<ListConversationsItemResponse> Conversations);

public sealed record ListConversationsItemResponse(
    Guid ConversationId,
    string Type,
    string? Name,
    IReadOnlyList<ListConversationsParticipantDto> Participants,
    DateTime CreatedAtUtc);

public sealed record ListConversationsParticipantDto(Guid UserId, string Username);
