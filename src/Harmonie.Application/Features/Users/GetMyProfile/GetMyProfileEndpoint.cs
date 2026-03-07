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
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] GetMyProfileHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<GetMyProfileResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
