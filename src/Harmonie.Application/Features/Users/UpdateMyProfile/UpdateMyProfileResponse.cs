namespace Harmonie.Application.Features.Users.UpdateMyProfile;

public sealed record UpdateMyProfileResponse(
    Guid UserId,
    string Username,
    string? DisplayName,
    string? Bio,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string Theme,
    string? Language);
