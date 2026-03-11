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
        app.MapPost("/api/uploads", HandleAsync)
            .WithName("UploadFile")
            .WithTags("Uploads")
            .RequireAuthorization()
            .DisableAntiforgery()
            .Accepts<UploadFileRequest>("multipart/form-data")
            .WithSummary("Upload a file")
            .WithDescription("Uploads a file to object storage and returns its metadata and public URL. " +
                "Optional form field 'purpose' accepts: attachment (default), guildIcon. " +
                "Avatar uploads must use the dedicated avatar endpoint.")
            .Produces<UploadFileResponse>(StatusCodes.Status201Created)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound,
                ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    private static async Task<IResult> HandleAsync(
        [FromForm] UploadFileRequest request,
        [FromServices] UploadFileHandler handler,
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
        var response = await handler.HandleAsync(
            fileName,
            contentType,
            file.Length,
            stream,
            currentUserId,
            purpose,
            cancellationToken);

        return response.ToCreatedHttpResult(data => $"/api/uploads/{data.FileId}");
    }
}
