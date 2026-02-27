namespace Harmonie.Application.Features.Auth.Logout;

/// <summary>
/// Request to logout the current authenticated session.
/// </summary>
public sealed record LogoutRequest(
    string RefreshToken);
