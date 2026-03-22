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
        app.MapPatch("/api/users/me", HandleAsync)
            .WithName("UpdateMyProfile")
            .WithTags("Users")
            .RequireAuthorization()
            .WithSummary("Update my profile")
            .WithDescription("Partially updates the authenticated user's profile. Omit a field to keep its current value; send null to clear a nullable field.")
            .WithJsonRequestBodyDocumentation(
                "Partial profile update. Omit a field to keep its current value. Send null for nullable fields to clear the value. Theme cannot be null.",
                typeof(UpdateMyProfileOpenApiRequest),
                (
                    "updateDisplayName",
                    "Update only the display name",
                    new
                    {
                        displayName = "Alice Harmonie"
                    }),
                (
                    "updateAvatarAppearance",
                    "Update avatar appearance",
                    new
                    {
                        avatar = new { color = "#FFF4D6", icon = "star", bg = "#1F2937" }
                    }),
                (
                    "updateThemeAndLanguage",
                    "Update theme and language",
                    new
                    {
                        theme = "dark",
                        language = "fr"
                    }),
                (
                    "clearProfileFields",
                    "Clear bio, avatar file, and avatar appearance",
                    new
                    {
                        bio = (string?)null,
                        avatarFileId = (string?)null,
                        avatar = (object?)null
                    }))
            .Produces<UpdateMyProfileResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateMyProfileRequest request,
        [FromServices] IAuthenticatedHandler<UpdateMyProfileRequest, UpdateMyProfileResponse> handler,
        [FromServices] IValidator<UpdateMyProfileRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateMyProfileResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }

    internal sealed record UpdateMyProfileOpenApiRequest(
        string? DisplayName,
        string? Bio,
        string? AvatarFileId,
        UpdateMyProfileOpenApiAvatarRequest? Avatar,
        string? Theme,
        string? Language);

    internal sealed record UpdateMyProfileOpenApiAvatarRequest(
        string? Color,
        string? Icon,
        string? Bg);
}
