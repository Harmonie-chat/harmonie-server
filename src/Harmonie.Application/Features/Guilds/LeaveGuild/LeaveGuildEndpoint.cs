using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.LeaveGuild;

public static class LeaveGuildEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/leave", HandleAsync)
            .WithName("LeaveGuild")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Leave a guild")
            .WithDescription("Removes the authenticated user from the guild. The guild owner cannot leave.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.OwnerCannotLeave);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromServices] IAuthenticatedHandler<LeaveGuildInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new LeaveGuildInput(guildId), currentUserId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
