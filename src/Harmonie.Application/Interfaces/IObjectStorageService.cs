namespace Harmonie.Application.Interfaces;

public sealed record ObjectStorageUploadRequest(
    string StorageKey,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record ObjectStorageUploadResult(
    bool Success,
    string? FailureReason)
{
    public static ObjectStorageUploadResult Succeeded() => new(true, null);

    public static ObjectStorageUploadResult Failed(string failureReason) => new(false, failureReason);
}

public interface IObjectStorageService
{
    Task<ObjectStorageUploadResult> UploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    string BuildPublicUrl(string storageKey);
}
