using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Uploads.UploadFile;

public static class UploadFileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/files/uploads", HandleAsync)
            .WithName("UploadFile")
            .WithTags("Files")
            .RequireAuthorization()
            .DisableAntiforgery()
            .Accepts<UploadFileRequest>("multipart/form-data")
            .WithSummary("Upload a file")
            .WithDescription("Uploads a file to object storage and returns its metadata. Optional `purpose` values are `attachment` and `guildIcon`. Use `/api/users/me/avatar` for avatar uploads.")
            .Produces<UploadFileResponse>(StatusCodes.Status201Created)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound,
                ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    private static async Task<IResult> HandleAsync(
        [FromForm] UploadFileRequest request,
        [FromServices] IAuthenticatedHandler<UploadFileInput, UploadFileResponse> handler,
        [FromServices] IValidator<UploadFileRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UploadFileResponse>.Fail(validationError).ToHttpResult();

        if (request.File is not IFormFile file)
        {
            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but file binding is missing.")
                .ToHttpResult();
        }

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var fileName = Path.GetFileName(file.FileName);
        var contentType = file.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
        {
            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but file metadata is invalid.")
                .ToHttpResult();
        }

        var purpose = UploadPurpose.Attachment;
        if (!string.IsNullOrWhiteSpace(request.Purpose))
            Enum.TryParse(request.Purpose, ignoreCase: true, out purpose);

        await using var stream = file.OpenReadStream();
        var input = new UploadFileInput(fileName, contentType, file.Length, stream, purpose);
        var response = await handler.HandleAsync(input, currentUserId, cancellationToken);

        return response.ToCreatedHttpResult(data => $"/api/files/{data.FileId}");
    }
}
