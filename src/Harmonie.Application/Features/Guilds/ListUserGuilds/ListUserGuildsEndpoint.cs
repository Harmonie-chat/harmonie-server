using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.ListUserGuilds;

public static class ListUserGuildsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds", HandleAsync)
            .WithName("ListUserGuilds")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List current user guilds")
            .WithDescription("Returns all guilds where the authenticated user is a member.")
            .Produces<ListUserGuildsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] ListUserGuildsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<ListUserGuildsResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
