namespace Harmonie.Application.Common.Auth;

/// <summary>
/// Internal authentication result containing the public response and the refresh cookie value.
/// </summary>
public sealed record AuthSessionResult<TResponse>(
    TResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
