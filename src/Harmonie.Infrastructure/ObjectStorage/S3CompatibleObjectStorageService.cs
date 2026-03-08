using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Harmonie.Infrastructure.ObjectStorage;

public sealed class S3CompatibleObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly ObjectStorageSettings _settings;
    private readonly ILogger<S3CompatibleObjectStorageService> _logger;
    private readonly AmazonS3Client? _client;
    private bool _bucketInitializationAttempted;
    private bool _bucketInitializationSucceeded;

    public S3CompatibleObjectStorageService(
        IOptions<ObjectStorageSettings> settings,
        ILogger<S3CompatibleObjectStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (!IsConfigured())
            return;

        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = _settings.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (!string.IsNullOrWhiteSpace(_settings.Region))
            config.AuthenticationRegion = _settings.Region;

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_settings.AccessKeyId, _settings.SecretAccessKey),
            config);
    }

    public async Task<ObjectStorageUploadResult> UploadAsync(
        ObjectStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = _client;
        if (client is null || !await EnsureReadyAsync(cancellationToken))
            return ObjectStorageUploadResult.Failed("Object storage is not configured.");

        try
        {
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = request.StorageKey,
                InputStream = request.Content,
                ContentType = request.ContentType,
                AutoCloseStream = false,
                AutoResetStreamPosition = false
            };

            if (request.SizeBytes >= 0)
                putObjectRequest.Headers.ContentLength = request.SizeBytes;

            await client.PutObjectAsync(putObjectRequest, cancellationToken);
            return ObjectStorageUploadResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Object storage upload failed. Bucket={BucketName}, Key={StorageKey}",
                _settings.BucketName,
                request.StorageKey);

            return ObjectStorageUploadResult.Failed("Object storage upload failed.");
        }
    }

    public async Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var client = _client;
        if (client is null || string.IsNullOrWhiteSpace(storageKey) || !await EnsureReadyAsync(cancellationToken))
            return;

        try
        {
            await client.DeleteObjectAsync(
                new DeleteObjectRequest
                {
                    BucketName = _settings.BucketName,
                    Key = storageKey
                },
                cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug(
                "Object storage cleanup skipped because key was not found. Bucket={BucketName}, Key={StorageKey}",
                _settings.BucketName,
                storageKey);
        }
    }

    public string BuildPublicUrl(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        var baseUrl = !string.IsNullOrWhiteSpace(_settings.PublicBaseUrl)
            ? _settings.PublicBaseUrl.TrimEnd('/')
            : $"{_settings.Endpoint.TrimEnd('/')}/{_settings.BucketName}";

        return $"{baseUrl}/{storageKey}";
    }

    private bool IsConfigured()
        => !string.IsNullOrWhiteSpace(_settings.Endpoint)
           && !string.IsNullOrWhiteSpace(_settings.BucketName)
           && !string.IsNullOrWhiteSpace(_settings.AccessKeyId)
           && !string.IsNullOrWhiteSpace(_settings.SecretAccessKey);

    private async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
            return false;

        if (_bucketInitializationAttempted)
            return _bucketInitializationSucceeded;

        _bucketInitializationAttempted = true;

        try
        {
            await EnsureBucketExistsAsync(client, cancellationToken);
            _bucketInitializationSucceeded = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Object storage initialization failed. Bucket={BucketName}, Endpoint={Endpoint}",
                _settings.BucketName,
                _settings.Endpoint);

            _bucketInitializationSucceeded = false;
            return false;
        }
    }

    private async Task EnsureBucketExistsAsync(
        IAmazonS3 client,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.GetBucketLocationAsync(
                new GetBucketLocationRequest
                {
                    BucketName = _settings.BucketName
                },
                cancellationToken);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == HttpStatusCode.NotFound ||
            string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.Ordinal))
        {
            if (!_settings.CreateBucketIfMissing)
            {
                _logger.LogWarning(
                    "Object storage bucket does not exist and auto-creation is disabled. Bucket={BucketName}",
                    _settings.BucketName);

                throw;
            }

            await CreateBucketAsync(client, cancellationToken);
        }
    }

    private async Task CreateBucketAsync(
        IAmazonS3 client,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.PutBucketAsync(
                new PutBucketRequest
                {
                    BucketName = _settings.BucketName,
                    UseClientRegion = true
                },
                cancellationToken);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == HttpStatusCode.Conflict ||
            string.Equals(ex.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.Ordinal) ||
            string.Equals(ex.ErrorCode, "BucketAlreadyExists", StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "Object storage bucket already exists. Bucket={BucketName}",
                _settings.BucketName);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
