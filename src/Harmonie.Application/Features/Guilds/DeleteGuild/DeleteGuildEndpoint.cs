using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.DeleteGuild;

public static class DeleteGuildEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/guilds/{guildId}", HandleAsync)
            .WithName("DeleteGuild")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Delete a guild")
            .WithDescription("Permanently deletes a guild and its associated members, channels, and messages. Only the guild owner can delete the guild.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromServices] DeleteGuildHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(guildId, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
