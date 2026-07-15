namespace Harmonie.Application.Features.Auth.Login;

/// <summary>
/// Response for successful login with an access token.
/// </summary>
public sealed record LoginResponse(
    Guid UserId,
    string Email,
    string Username,
    string AccessToken,
    DateTime ExpiresAt);
