namespace Harmonie.Application.Features.Users.GetMyProfile;

public sealed record GetMyProfileResponse(
    Guid UserId,
    string Username,
    string? DisplayName,
    string? Bio,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string Theme,
    string? Language,
    string Status);
