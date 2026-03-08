namespace Harmonie.Infrastructure.Configuration;

public sealed class ObjectStorageSettings
{
    public string LocalBasePath { get; init; } = "uploads";
    public string LocalBaseUrl { get; init; } = "http://localhost:5000/files";
}
