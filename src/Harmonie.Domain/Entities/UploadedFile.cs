using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class UploadedFile : Entity<UploadedFileId>
{
    public UserId UploaderUserId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; }

    private UploadedFile(
        UploadedFileId id,
        UserId uploaderUserId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        DateTime createdAtUtc)
    {
        Id = id;
        UploaderUserId = uploaderUserId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<UploadedFile> Create(
        UserId uploaderUserId,
        string? fileName,
        string? contentType,
        long sizeBytes,
        string? storageKey)
    {
        if (uploaderUserId is null)
            return Result.Failure<UploadedFile>("Uploader user ID is required");

        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<UploadedFile>("File name is required");

        var normalizedFileName = fileName.Trim();
        if (normalizedFileName.Length > 255)
            return Result.Failure<UploadedFile>("File name cannot exceed 255 characters");

        if (string.IsNullOrWhiteSpace(contentType))
            return Result.Failure<UploadedFile>("Content type is required");

        var normalizedContentType = contentType.Trim();
        if (normalizedContentType.Length > 255)
            return Result.Failure<UploadedFile>("Content type cannot exceed 255 characters");

        if (sizeBytes <= 0)
            return Result.Failure<UploadedFile>("File size must be greater than zero");

        if (string.IsNullOrWhiteSpace(storageKey))
            return Result.Failure<UploadedFile>("Storage key is required");

        var normalizedStorageKey = storageKey.Trim();
        if (normalizedStorageKey.Length > 1024)
            return Result.Failure<UploadedFile>("Storage key cannot exceed 1024 characters");

        return Result.Success(new UploadedFile(
            UploadedFileId.New(),
            uploaderUserId,
            normalizedFileName,
            normalizedContentType,
            sizeBytes,
            normalizedStorageKey,
            DateTime.UtcNow));
    }

    public static UploadedFile Rehydrate(
        UploadedFileId id,
        UserId uploaderUserId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(uploaderUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size must be greater than zero.");

        return new UploadedFile(
            id,
            uploaderUserId,
            fileName,
            contentType,
            sizeBytes,
            storageKey,
            createdAtUtc);
    }
}
