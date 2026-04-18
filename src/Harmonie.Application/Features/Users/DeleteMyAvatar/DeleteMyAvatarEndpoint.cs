using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Users.DeleteMyAvatar;

public static class DeleteMyAvatarEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/me/avatar", HandleAsync)
            .WithName("DeleteMyAvatar")
            .WithTags("Users")
            .RequireAuthorization()
            .WithSummary("Delete my avatar")
            .WithDescription("Removes the authenticated user's uploaded avatar and falls back to the default avatar.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound,
                ApplicationErrorCodes.Upload.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IAuthenticatedHandler<Unit, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(Unit.Value, currentUserId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
