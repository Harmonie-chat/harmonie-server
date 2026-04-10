using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Users.SearchUsers;

public sealed record SearchUsersResponse(
    IReadOnlyList<SearchUsersItemResponse> Users);

public sealed record SearchUsersItemResponse(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string? Bio,
    string Status);
