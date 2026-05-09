using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Conversations.GetConversationParticipants;

public sealed record GetConversationParticipantsResponse(
    IReadOnlyList<GetConversationParticipantsItem> Participants);

public sealed record GetConversationParticipantsItem(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    DateTime JoinedAtUtc);
