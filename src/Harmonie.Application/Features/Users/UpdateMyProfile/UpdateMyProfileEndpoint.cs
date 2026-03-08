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
            .Accepts<UpdateMyProfileOpenApiRequest>("application/json")
            .Produces<UpdateMyProfileResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound);
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

    internal sealed record UpdateMyProfileOpenApiRequest(
        string? DisplayName,
        string? Bio,
        string? AvatarUrl);
}
