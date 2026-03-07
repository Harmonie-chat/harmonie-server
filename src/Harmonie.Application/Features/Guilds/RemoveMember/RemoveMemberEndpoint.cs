using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
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
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.MemberNotFound,
                ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] RemoveMemberRouteRequest routeRequest,
        [FromServices] RemoveMemberHandler handler,
        [FromServices] IValidator<RemoveMemberRouteRequest> routeValidator,
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

        if (routeRequest.UserId is not string userIdStr
            || !UserId.TryParse(userIdStr, out var parsedTargetId)
            || parsedTargetId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but user ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedGuildId, callerId, parsedTargetId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
