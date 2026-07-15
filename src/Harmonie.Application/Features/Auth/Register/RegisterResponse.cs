using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Auth.Register;

/// <summary>
/// Response for successful user registration with an access token.
/// </summary>
public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string Username,
    string AccessToken,
    DateTime ExpiresAt,
    AvatarAppearanceDto? Avatar,
    string Theme);
