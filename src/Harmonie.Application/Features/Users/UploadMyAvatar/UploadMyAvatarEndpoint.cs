using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Users.UploadMyAvatar;

public static class UploadMyAvatarEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/me/avatar", HandleAsync)
            .WithName("UploadMyAvatar")
            .WithTags("Users")
            .RequireAuthorization()
            .DisableAntiforgery()
            .Accepts<UploadMyAvatarRequest>("multipart/form-data")
            .WithSummary("Upload user avatar")
            .WithDescription("Uploads and sets the authenticated user's avatar image. The image is resized to 256x256. Replaces current avatar.")
            .Produces<UploadMyAvatarResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound,
                ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    private static async Task<IResult> HandleAsync(
        [FromForm] UploadMyAvatarRequest request,
        [FromServices] IAuthenticatedHandler<UploadMyAvatarInput, UploadMyAvatarResponse> handler,
        [FromServices] IValidator<UploadMyAvatarRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UploadMyAvatarResponse>.Fail(validationError).ToHttpResult(httpContext);

        if (request.File is not IFormFile file)
        {
            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but file binding is missing.")
                .ToHttpResult(httpContext);
        }

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var fileName = Path.GetFileName(file.FileName);
        var contentType = file.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
        {
            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but file metadata is invalid.")
                .ToHttpResult(httpContext);
        }

        await using var stream = file.OpenReadStream();
        var input = new UploadMyAvatarInput(fileName, contentType, stream);
        var response = await handler.HandleAsync(
            input,
            currentUserId,
            cancellationToken);

        return response.ToHttpResult(httpContext);
    }
}
