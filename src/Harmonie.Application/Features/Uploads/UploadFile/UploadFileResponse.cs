namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed record UploadFileResponse(
    string FileId,
    string Url,
    string Filename,
    string ContentType,
    long SizeBytes);
