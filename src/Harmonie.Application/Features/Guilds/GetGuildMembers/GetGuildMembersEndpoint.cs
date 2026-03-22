using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public static class GetGuildMembersEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/members", HandleAsync)
            .WithName("GetGuildMembers")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List guild members")
            .WithDescription("Returns guild members for an authenticated guild member.")
            .Produces<GetGuildMembersResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromServices] IAuthenticatedHandler<GuildId, GetGuildMembersResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(guildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
