using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Auth.Register;

/// <summary>
/// Request to register a new user account
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Username,
    string Password,
    AvatarAppearanceDto? Avatar = null,
    string? Theme = null);
