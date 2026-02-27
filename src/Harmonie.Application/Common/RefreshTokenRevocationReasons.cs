namespace Harmonie.Application.Common;

/// <summary>
/// Standardized refresh token revocation reasons persisted in storage.
/// </summary>
public static class RefreshTokenRevocationReasons
{
    public const string Rotated = "rotated";
    public const string Logout = "logout";
    public const string LogoutAll = "logout_all";
    public const string ReuseDetected = "reuse_detected";
}
