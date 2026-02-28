using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Users.UpdateMyProfile;

public static class UpdateMyProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/me", HandleAsync)
            .WithName("UpdateMyProfile")
            .WithTags("Users")
            .RequireAuthorization()
            .WithSummary("Update my profile")
            .WithDescription("Updates display name, bio, and avatar URL for the authenticated user.")
            .Produces<UpdateMyProfileResponse>(StatusCodes.Status200OK)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateMyProfileRequest request,
        [FromServices] UpdateMyProfileHandler handler,
        [FromServices] IValidator<UpdateMyProfileRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateMyProfileResponse>.Fail(validationError).ToHttpResult();

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<UpdateMyProfileResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
