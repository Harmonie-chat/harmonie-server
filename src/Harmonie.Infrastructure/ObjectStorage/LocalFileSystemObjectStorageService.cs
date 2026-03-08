using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Infrastructure.ObjectStorage;

public sealed class LocalFileSystemObjectStorageService : IObjectStorageService
{
    private readonly string _basePath;
    private readonly string _basePathWithSeparator;
    private readonly string _baseUrl;
    private readonly ILogger<LocalFileSystemObjectStorageService> _logger;

    public LocalFileSystemObjectStorageService(
        IOptions<ObjectStorageSettings> settings,
        ILogger<LocalFileSystemObjectStorageService> logger)
    {
        var s = settings.Value;
        _basePath = Path.GetFullPath(
            Path.IsPathRooted(s.LocalBasePath)
                ? s.LocalBasePath
                : Path.Combine(AppContext.BaseDirectory, s.LocalBasePath));
        _basePathWithSeparator = _basePath.EndsWith(Path.DirectorySeparatorChar)
            ? _basePath
            : _basePath + Path.DirectorySeparatorChar;

        if (string.IsNullOrWhiteSpace(s.LocalBaseUrl))
            throw new InvalidOperationException("ObjectStorage:LocalBaseUrl is required when provider is local.");

        _baseUrl = s.LocalBaseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<ObjectStorageUploadResult> UploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var fullPath = ResolveFullPath(request.StorageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await request.Content.CopyToAsync(fileStream, cancellationToken);
            return ObjectStorageUploadResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Local filesystem upload failed. StorageKey={StorageKey}",
                request.StorageKey);

            return ObjectStorageUploadResult.Failed("Local filesystem upload failed.");
        }
    }

    public Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return Task.CompletedTask;

        try
        {
            var fullPath = ResolveFullPath(storageKey);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Local filesystem cleanup failed. StorageKey={StorageKey}",
                storageKey);
        }

        return Task.CompletedTask;
    }

    public string BuildPublicUrl(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        return $"{_baseUrl}/{storageKey}";
    }

    private string ResolveFullPath(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var normalizedStorageKey = storageKey
            .Replace('\\', '/')
            .TrimStart('/');
        var candidatePath = Path.GetFullPath(
            Path.Combine(_basePath, normalizedStorageKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!candidatePath.StartsWith(_basePathWithSeparator, StringComparison.Ordinal)
            && !string.Equals(candidatePath, _basePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved storage path escapes the configured local base path.");
        }

        return candidatePath;
    }
}
