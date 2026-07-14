namespace Harmonie.Application.Interfaces.Notifications;

public interface INotificationCleanupRepository
{
    Task<int> DeleteOutboxBatchAsync(
        string status,
        DateTime processedBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredDevicesBatchAsync(
        DateTime expiresBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}
