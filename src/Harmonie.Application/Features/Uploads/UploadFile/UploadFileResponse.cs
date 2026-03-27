namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed record UploadFileResponse(
    Guid FileId,
    string Filename,
    string ContentType,
    long SizeBytes);
