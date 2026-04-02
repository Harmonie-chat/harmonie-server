using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Uploads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Uploads.DeleteFile;

public static class DeleteFileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/files/{fileId}", HandleAsync)
            .WithName("DeleteFile")
            .WithTags("Files")
            .RequireAuthorization()
            .WithSummary("Delete an uploaded file")
            .WithDescription("Permanently deletes an uploaded file from storage. Only the uploader can delete their own files.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Upload.NotFound,
                ApplicationErrorCodes.Upload.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        UploadedFileId fileId,
        [FromServices] IAuthenticatedHandler<DeleteFileInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new DeleteFileInput(fileId), currentUserId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
