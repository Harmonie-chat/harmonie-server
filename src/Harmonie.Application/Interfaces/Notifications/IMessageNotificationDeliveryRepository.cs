namespace Harmonie.Application.Interfaces.Notifications;

public static class MessageNotificationDeliveryStatuses
{
    public const string Pending = "pending";
    public const string Succeeded = "succeeded";
    public const string TransientFailure = "transient_failure";
    public const string InvalidDevice = "invalid_device";
    public const string Failed = "failed";
}

public sealed record MessageNotificationDelivery(
    Guid Id,
    Guid OutboxJobId,
    Guid DeviceId,
    string Status,
    int Attempts,
    string? LastError,
    DateTime? FirstAttemptedAtUtc,
    DateTime? LastAttemptedAtUtc,
    DateTime? SucceededAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public interface IMessageNotificationDeliveryRepository
{
    Task EnsurePendingAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageNotificationDelivery>> GetByJobAndDeviceIdsAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default);

    Task MarkResultsAsync(
        Guid outboxJobId,
        IReadOnlyCollection<NotificationDeliveryResult> results,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkDevicesFailedAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        string error,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default);
}
