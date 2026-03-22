using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.UnbanMember;

public static class UnbanMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/guilds/{guildId}/bans/{userId}", HandleAsync)
            .WithName("UnbanMember")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Unban a member from a guild")
            .WithDescription("Removes the ban for the specified user, allowing them to rejoin via invite. Only admins can unban.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.NotBanned);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        UserId userId,
        [FromServices] UnbanMemberHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            guildId,
            callerId,
            userId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
