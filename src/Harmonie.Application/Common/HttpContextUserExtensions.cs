using System.Security.Claims;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace Harmonie.Application.Common;

public static class HttpContextUserExtensions
{
    public static bool TryGetAuthenticatedUserId(
        this HttpContext httpContext,
        out UserId? userId)
    {
        userId = null;

        if (httpContext is null)
            return false;

        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
            return false;

        var claimValue = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(claimValue))
            return false;

        if (!UserId.TryParse(claimValue, out var parsedUserId) || parsedUserId is null)
            return false;

        userId = parsedUserId;
        return true;
    }

    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType)
    {
        if (principal is null)
            return null;

        return principal.FindFirst(claimType)?.Value;
    }
}
