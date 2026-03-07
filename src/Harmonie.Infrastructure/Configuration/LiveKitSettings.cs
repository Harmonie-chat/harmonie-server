namespace Harmonie.Infrastructure.Configuration;

public sealed class LiveKitSettings
{
    public string Url { get; init; } = null!;
    public string ApiKey { get; init; } = null!;
    public string ApiSecret { get; init; } = null!;
}
