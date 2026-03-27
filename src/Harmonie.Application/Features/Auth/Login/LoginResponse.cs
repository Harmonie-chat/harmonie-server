namespace Harmonie.Application.Features.Auth.Login;

/// <summary>
/// Response for successful login with authentication tokens
/// </summary>
public sealed record LoginResponse(
    Guid UserId,
    string Email,
    string Username,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
