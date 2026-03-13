using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.RevokeInvite;

public static class RevokeInviteEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/guilds/{guildId}/invites/{inviteCode}", HandleAsync)
            .WithName("RevokeInvite")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Revoke a guild invite link")
            .WithDescription("Soft-deletes an invite by setting its revoked_at_utc timestamp. Only guild administrators or the invite creator can revoke it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Invite.NotFound,
                ApplicationErrorCodes.Invite.RevokeForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] RevokeInviteRouteRequest routeRequest,
        [FromServices] RevokeInviteHandler handler,
        [FromServices] IValidator<RevokeInviteRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.GuildId is not string guildIdStr
            || !GuildId.TryParse(guildIdStr, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(parsedGuildId, routeRequest.InviteCode!, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
