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
        [FromServices] IAuthenticatedHandler<Unit, LogoutAllResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(Unit.Value, currentUserId, cancellationToken);
        if (!response.Success)
            return response.ToHttpResult(httpContext);

        return Results.NoContent();
    }
}
