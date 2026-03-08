using Microsoft.AspNetCore.Http;

namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed class UploadFileRequest
{
    public IFormFile? File { get; init; }
}
