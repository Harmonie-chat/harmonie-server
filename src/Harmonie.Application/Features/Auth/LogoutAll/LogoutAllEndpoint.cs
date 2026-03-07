using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Auth.LogoutAll;

/// <summary>
/// Endpoint for logging out all active authenticated sessions.
/// </summary>
public static class LogoutAllEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout-all", HandleAsync)
            .WithName("LogoutAll")
            .WithTags("Auth")
            .RequireAuthorization()
            .WithSummary("Logout all sessions")
            .WithDescription("Revokes all active refresh tokens for the authenticated user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] LogoutAllHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<LogoutAllResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(currentUserId, cancellationToken);
        if (!response.Success)
            return response.ToHttpResult();

        return Results.NoContent();
    }
}
