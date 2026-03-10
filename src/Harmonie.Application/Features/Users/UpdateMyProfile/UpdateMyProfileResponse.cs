namespace Harmonie.Application.Features.Users.UpdateMyProfile;

public sealed record UpdateMyProfileResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    AvatarAppearanceDto? Avatar,
    string Theme,
    string? Language);
