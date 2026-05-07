using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Conversations.ListConversations;

public sealed record ListConversationsResponse(
    IReadOnlyList<ListConversationsItemResponse> Conversations);

public sealed record ListConversationsItemResponse(
    Guid ConversationId,
    string Type,
    string? Name,
    IReadOnlyList<ListConversationsParticipantDto> Participants,
    DateTime CreatedAtUtc,
    bool HasUnread);

public sealed record ListConversationsParticipantDto(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar);
