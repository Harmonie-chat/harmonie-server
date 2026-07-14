using Microsoft.AspNetCore.Http;

namespace Harmonie.Application.Common.Auth;

public static class RefreshTokenCookie
{
    public const string Name = "__Host-harmonie-refresh";

    public static string? Read(HttpContext httpContext) =>
        httpContext.Request.Cookies.TryGetValue(Name, out var refreshToken)
            ? refreshToken
            : null;

    public static void Write(HttpContext httpContext, string refreshToken, DateTime expiresAtUtc)
    {
        httpContext.Response.Cookies.Append(
            Name,
            refreshToken,
            CreateOptions(new DateTimeOffset(EnsureUtc(expiresAtUtc))));
    }

    public static void Delete(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(Name, CreateOptions(expires: null));
    }

    private static CookieOptions CreateOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = "/",
        IsEssential = true,
        Expires = expires
    };

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
