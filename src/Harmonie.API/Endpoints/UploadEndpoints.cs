using Harmonie.Application.Features.Uploads.DeleteFile;
using Harmonie.Application.Features.Uploads.DownloadFile;
using Harmonie.Application.Features.Uploads.UploadFile;

namespace Harmonie.API.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        UploadFileEndpoint.Map(app);
        DownloadFileEndpoint.Map(app);
        DeleteFileEndpoint.Map(app);
    }
}
