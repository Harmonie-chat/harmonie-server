namespace Harmonie.Infrastructure.Configuration;

public sealed class ObjectStorageSettings
{
    /// <summary>Storage backend to use. Accepted values: "s3" (default), "local".</summary>
    public string Provider { get; init; } = "s3";

    // S3 provider settings
    public string Endpoint { get; init; } = string.Empty;
    public string PublicBaseUrl { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-1";
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;
    public bool CreateBucketIfMissing { get; init; } = true;

    // Local filesystem provider settings
    public string LocalBasePath { get; init; } = "uploads";
    public string LocalBaseUrl { get; init; } = string.Empty;
}
