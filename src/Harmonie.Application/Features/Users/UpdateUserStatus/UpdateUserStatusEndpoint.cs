using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Users.UpdateUserStatus;

public static class UpdateUserStatusEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/me/status", HandleAsync)
            .WithName("UpdateUserStatus")
            .WithTags("Users")
            .RequireAuthorization()
            .WithSummary("Update my presence status")
            .WithDescription("Set the authenticated user's presence status. Valid statuses: online, idle, dnd, invisible. The preference is persisted and survives reconnection. Invisible users appear offline to others but still receive events.")
            .WithJsonRequestBodyDocumentation(
                "Presence status update.",
                (
                    "setDnd",
                    "Set status to Do Not Disturb",
                    new { status = "dnd" }),
                (
                    "setInvisible",
                    "Set status to invisible (appear offline)",
                    new { status = "invisible" }))
            .Produces<UpdateUserStatusResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateUserStatusRequest request,
        [FromServices] UpdateUserStatusHandler handler,
        [FromServices] IValidator<UpdateUserStatusRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateUserStatusResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
