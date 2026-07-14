using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Notifications;

public static class NotificationDevicePlatforms
{
    public const string WebPush = "web_push";
}

public sealed record NotificationDevice(
    Guid Id,
    UserId UserId,
    string Platform,
    string Token,
    string? WebPushP256dh,
    string? WebPushAuth,
    DateTime? ExpiresAtUtc);

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

    Task<IReadOnlyList<NotificationDevice>> GetActiveByUserIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task DeleteManyAsync(
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default);
}
