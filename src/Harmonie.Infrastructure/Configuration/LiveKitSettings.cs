namespace Harmonie.Infrastructure.Configuration;

public sealed class LiveKitSettings
{
    public string PublicUrl { get; init; } = string.Empty;
    public string InternalUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;

    public string GetInternalUrl()
        => string.IsNullOrWhiteSpace(InternalUrl)
            ? PublicUrl
            : InternalUrl;
}
