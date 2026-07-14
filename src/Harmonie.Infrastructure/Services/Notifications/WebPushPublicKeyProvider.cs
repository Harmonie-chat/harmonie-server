using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Harmonie.Infrastructure.Services.Notifications;

public sealed class WebPushPublicKeyProvider : IWebPushPublicKeyProvider
{
    private readonly WebPushSettings _settings;

    public WebPushPublicKeyProvider(IOptions<WebPushSettings> settings)
    {
        _settings = settings.Value;
    }

    public string? GetPublicKey()
    {
        return string.IsNullOrWhiteSpace(_settings.PublicKey)
            ? null
            : _settings.PublicKey;
    }
}
