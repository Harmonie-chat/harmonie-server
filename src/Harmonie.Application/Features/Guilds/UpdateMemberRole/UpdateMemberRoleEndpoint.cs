using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public static class UpdateMemberRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/guilds/{guildId}/members/{userId}/role", HandleAsync)
            .WithName("UpdateMemberRole")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Update a guild member's role")
            .WithDescription("Changes the role of the specified member. Only admins can change roles. The guild owner's role cannot be changed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.MemberNotFound,
                ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] UpdateMemberRoleRouteRequest routeRequest,
        [FromBody] UpdateMemberRoleRequest request,
        [FromServices] UpdateMemberRoleHandler handler,
        [FromServices] IValidator<UpdateMemberRoleRouteRequest> routeValidator,
        [FromServices] IValidator<UpdateMemberRoleRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<bool>.Fail(validationError).ToHttpResult();

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

        var response = await handler.HandleAsync(parsedGuildId, callerId, parsedTargetId, request.Role.ToDomain(), cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
