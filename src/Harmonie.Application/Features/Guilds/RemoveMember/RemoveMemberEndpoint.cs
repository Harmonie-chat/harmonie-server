using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.RemoveMember;

public static class RemoveMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/guilds/{guildId}/members/{userId}", HandleAsync)
            .WithName("RemoveMember")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Remove a member from a guild")
            .WithDescription("Removes the specified user from the guild. Only admins can remove members. The guild owner cannot be removed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.MemberNotFound,
                ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        UserId userId,
        [FromServices] IAuthenticatedHandler<RemoveMemberInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new RemoveMemberInput(guildId, userId), callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
