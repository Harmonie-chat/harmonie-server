using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Notifications;

public sealed record WebPushNotificationDeviceRegistration(
    UserId UserId,
    string Endpoint,
    string P256dh,
    string Auth,
    DateTime? ExpiresAtUtc);

public interface INotificationDeviceRepository
{
    Task UpsertWebPushAsync(
        WebPushNotificationDeviceRegistration registration,
        CancellationToken cancellationToken = default);
}
