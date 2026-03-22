using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public static class UpdateMemberRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/guilds/{guildId}/members/{userId}/role", HandleAsync)
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
        GuildId guildId,
        UserId userId,
        [FromBody] UpdateMemberRoleRequest request,
        [FromServices] UpdateMemberRoleHandler handler,
        [FromServices] IValidator<UpdateMemberRoleRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<bool>.Fail(validationError).ToHttpResult();

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(guildId, callerId, userId, request.Role.ToDomain(), cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
