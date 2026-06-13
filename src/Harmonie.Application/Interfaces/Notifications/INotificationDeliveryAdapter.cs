namespace Harmonie.Application.Interfaces.Notifications;

public enum NotificationDeliveryResultStatus
{
    Succeeded,
    InvalidDevice,
    TransientFailure,
    PermanentFailure
}

public sealed record NotificationDeliveryPayload(
    string Title,
    string Body,
    string TargetUrl,
    string Tag,
    string Icon,
    string Badge);

public sealed record NotificationDeliveryResult(
    Guid DeviceId,
    NotificationDeliveryResultStatus Status,
    string? Error = null);

public interface INotificationDeliveryAdapter
{
    string Platform { get; }

    Task<IReadOnlyList<NotificationDeliveryResult>> SendAsync(
        NotificationDeliveryPayload payload,
        IReadOnlyList<NotificationDevice> devices,
        CancellationToken cancellationToken = default);
}
