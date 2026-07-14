using System.Net;
using System.Text.Json;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace Harmonie.Infrastructure.Services.Notifications;

public sealed class WebPushNotificationDeliveryAdapter : INotificationDeliveryAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly WebPushClient _client;
    private readonly WebPushEndpointValidator _endpointValidator;
    private readonly WebPushSettings _settings;
    private readonly ILogger<WebPushNotificationDeliveryAdapter> _logger;

    public WebPushNotificationDeliveryAdapter(
        WebPushClient client,
        WebPushEndpointValidator endpointValidator,
        IOptions<WebPushSettings> settings,
        ILogger<WebPushNotificationDeliveryAdapter> logger)
    {
        _client = client;
        _endpointValidator = endpointValidator;
        _settings = settings.Value;
        _logger = logger;
    }

    public string Platform => NotificationDevicePlatforms.WebPush;

    public async Task<IReadOnlyList<NotificationDeliveryResult>> SendAsync(
        NotificationDeliveryPayload payload,
        IReadOnlyList<NotificationDevice> devices,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.HasVapidCredentials)
        {
            return devices
                .Select(device => new NotificationDeliveryResult(
                    device.Id,
                    NotificationDeliveryResultStatus.TransientFailure,
                    "Web Push VAPID credentials are not configured"))
                .ToArray();
        }

        var serializedPayload = JsonSerializer.Serialize(payload, SerializerOptions);
        var vapidDetails = new VapidDetails(_settings.Subject, _settings.PublicKey, _settings.PrivateKey);
        var results = new List<NotificationDeliveryResult>(devices.Count);

        foreach (var device in devices)
        {
            var validationResult = await ValidateDeviceAsync(device, cancellationToken);
            if (validationResult is not null)
            {
                results.Add(validationResult);
                continue;
            }

            var subscription = new PushSubscription(device.Token, device.WebPushP256dh, device.WebPushAuth);
            results.Add(await SendToDeviceAsync(device.Id, subscription, serializedPayload, vapidDetails, cancellationToken));
        }

        return results;
    }

    private async Task<NotificationDeliveryResult?> ValidateDeviceAsync(
        NotificationDevice device,
        CancellationToken cancellationToken)
    {
        if (device.WebPushP256dh is null || device.WebPushAuth is null)
        {
            return new NotificationDeliveryResult(
                device.Id,
                NotificationDeliveryResultStatus.InvalidDevice,
                "Web Push device is missing encryption keys");
        }

        var endpointAllowed = await _endpointValidator.IsAllowedAsync(device.Token, cancellationToken);
        if (!endpointAllowed)
        {
            return new NotificationDeliveryResult(
                device.Id,
                NotificationDeliveryResultStatus.PermanentFailure,
                "Web Push endpoint is not allowed");
        }

        return null;
    }

    private async Task<NotificationDeliveryResult> SendToDeviceAsync(
        Guid deviceId,
        PushSubscription subscription,
        string payload,
        VapidDetails vapidDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            await _client.SendNotificationAsync(subscription, payload, vapidDetails, cancellationToken);
            return new NotificationDeliveryResult(deviceId, NotificationDeliveryResultStatus.Succeeded);
        }
        catch (WebPushException ex) when (IsInvalidDeviceStatus(ex.StatusCode))
        {
            return new NotificationDeliveryResult(
                deviceId,
                NotificationDeliveryResultStatus.InvalidDevice,
                $"Web Push subscription is no longer valid or no longer accepts this VAPID identity ({(int)ex.StatusCode})");
        }
        catch (WebPushException ex) when (IsTransient(ex.StatusCode))
        {
            return new NotificationDeliveryResult(
                deviceId,
                NotificationDeliveryResultStatus.TransientFailure,
                $"Web Push send failed with transient status {(int)ex.StatusCode}");
        }
        catch (WebPushException ex)
        {
            return new NotificationDeliveryResult(
                deviceId,
                NotificationDeliveryResultStatus.PermanentFailure,
                $"Web Push send failed with status {(int)ex.StatusCode}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected Web Push send failure for notification device {DeviceId}", deviceId);
            return new NotificationDeliveryResult(
                deviceId,
                NotificationDeliveryResultStatus.TransientFailure,
                "Unexpected Web Push send failure");
        }
    }

    private static bool IsInvalidDeviceStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.NotFound
            or HttpStatusCode.Gone;
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return numericStatusCode == 408 || numericStatusCode == 429 || numericStatusCode >= 500;
    }
}
