using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Uploads;
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
        UploadedFileId fileId,
        [FromServices] IAuthenticatedHandler<UploadedFileId, DownloadFileResult> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            fileId,
            currentUserId,
            cancellationToken);

        if (!response.Success || response.Data is null)
            return response.ToHttpResult(httpContext);

        return Results.File(
            response.Data.Content,
            response.Data.ContentType,
            response.Data.FileName);
    }
}
