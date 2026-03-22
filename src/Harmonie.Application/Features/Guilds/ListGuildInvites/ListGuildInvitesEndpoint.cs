using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.ListGuildInvites;

public static class ListGuildInvitesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/invites", HandleAsync)
            .WithName("ListGuildInvites")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List guild invite links")
            .WithDescription("Returns all invite links for a guild, including expired ones. Admin only.")
            .Produces<ListGuildInvitesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.InviteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromServices] ListGuildInvitesHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(guildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
