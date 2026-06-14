namespace Harmonie.Infrastructure.Configuration;

public sealed class WebPushSettings
{
    public const string SectionName = "WebPush";

    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public bool HasVapidCredentials =>
        !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(PrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
