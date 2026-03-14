namespace Harmonie.Application.Features.Users.GetMyProfile;

public sealed record GetMyProfileResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? Bio,
    string? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string Theme,
    string? Language,
    string Status);
