using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Uploads.DownloadFile;

public static class DownloadFileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files/{fileId}", HandleAsync)
            .WithName("DownloadFile")
            .WithTags("Files")
            .RequireAuthorization()
            .WithSummary("Download a file")
            .WithDescription("Downloads a file by its ID. Requires authentication.")
            .Produces(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Upload.NotFound,
                ApplicationErrorCodes.Upload.AccessDenied,
                ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string fileId,
        [FromServices] DownloadFileHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!UploadedFileId.TryParse(fileId, out var parsedFileId) || parsedFileId is null)
        {
            return ApplicationResponse<DownloadFileResult>.Fail(
                ApplicationErrorCodes.Upload.NotFound,
                "File was not found")
                .ToHttpResult();
        }

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            parsedFileId,
            currentUserId,
            cancellationToken);

        if (!response.Success || response.Data is null)
            return response.ToHttpResult();

        return Results.File(
            response.Data.Content,
            response.Data.ContentType,
            response.Data.FileName);
    }
}
