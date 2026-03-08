namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed class UploadOptions
{
    public long MaxFileSizeBytes { get; init; } = 25L * 1024 * 1024;
}
