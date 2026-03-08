namespace Harmonie.Application.Features.Users.SearchUsers;

public sealed record SearchUsersResponse(
    IReadOnlyList<SearchUsersItemResponse> Users);

public sealed record SearchUsersItemResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string Status);
