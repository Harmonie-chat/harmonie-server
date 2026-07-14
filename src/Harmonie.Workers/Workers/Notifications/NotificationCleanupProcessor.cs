using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Workers.Workers.Notifications;

public sealed class NotificationCleanupProcessor : INotificationCleanupProcessor
{
    private readonly INotificationCleanupRepository _cleanupRepository;
    private readonly NotificationCleanupOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationCleanupProcessor> _logger;

    public NotificationCleanupProcessor(
        INotificationCleanupRepository cleanupRepository,
        IOptions<NotificationCleanupOptions> options,
        TimeProvider timeProvider,
        ILogger<NotificationCleanupProcessor> logger)
    {
        _cleanupRepository = cleanupRepository;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var processedBeforeUtc = nowUtc.AddDays(-_options.ProcessedOutboxRetentionDays);
        var failedBeforeUtc = nowUtc.AddDays(-_options.FailedOutboxRetentionDays);
        var expiresBeforeUtc = nowUtc.AddDays(-_options.ExpiredDeviceRetentionDays);

        var processedCount = await DeleteAllBatchesAsync(
            MessageNotificationOutboxStatuses.Processed,
            processedBeforeUtc,
            cancellationToken);
        _logger.LogInformation(
            "Notification cleanup deleted {DeletedRowCount} outbox rows with status {Status}.",
            processedCount,
            MessageNotificationOutboxStatuses.Processed);

        var failedCount = await DeleteAllBatchesAsync(
            MessageNotificationOutboxStatuses.Failed,
            failedBeforeUtc,
            cancellationToken);
        _logger.LogInformation(
            "Notification cleanup deleted {DeletedRowCount} outbox rows with status {Status}.",
            failedCount,
            MessageNotificationOutboxStatuses.Failed);

        var expiredDeviceCount = await DeleteExpiredDeviceBatchesAsync(
            expiresBeforeUtc,
            cancellationToken);
        _logger.LogInformation(
            "Notification cleanup deleted {DeletedRowCount} expired notification devices.",
            expiredDeviceCount);
    }

    private async Task<int> DeleteAllBatchesAsync(
        string status,
        DateTime processedBeforeUtc,
        CancellationToken cancellationToken)
    {
        var deletedCount = 0;
        int deletedInBatch;

        do
        {
            deletedInBatch = await _cleanupRepository.DeleteOutboxBatchAsync(
                status,
                processedBeforeUtc,
                _options.BatchSize,
                cancellationToken);
            deletedCount += deletedInBatch;
        }
        while (deletedInBatch > 0);

        return deletedCount;
    }

    private async Task<int> DeleteExpiredDeviceBatchesAsync(
        DateTime expiresBeforeUtc,
        CancellationToken cancellationToken)
    {
        var deletedCount = 0;
        int deletedInBatch;

        do
        {
            deletedInBatch = await _cleanupRepository.DeleteExpiredDevicesBatchAsync(
                expiresBeforeUtc,
                _options.BatchSize,
                cancellationToken);
            deletedCount += deletedInBatch;
        }
        while (deletedInBatch > 0);

        return deletedCount;
    }
}
