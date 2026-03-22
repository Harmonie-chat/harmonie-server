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
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IAuthenticatedHandler<Unit, ListUserGuildsResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(Unit.Value, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
