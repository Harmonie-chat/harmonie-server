using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Users.GetMyProfile;

public static class GetMyProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/me", HandleAsync)
            .WithName("GetMyProfile")
            .WithTags("Users")
            .RequireAuthorization()
            .WithSummary("Get my profile")
            .WithDescription("Returns the authenticated user's profile.")
            .Produces<GetMyProfileResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IAuthenticatedHandler<Unit, GetMyProfileResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(Unit.Value, currentUserId, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
