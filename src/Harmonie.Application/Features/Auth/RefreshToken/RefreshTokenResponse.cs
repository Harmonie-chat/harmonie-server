namespace Harmonie.Application.Features.Auth.RefreshToken;

/// <summary>
/// Response with a refreshed access token.
/// </summary>
public sealed record RefreshTokenResponse(
    string AccessToken,
    DateTime ExpiresAt);
