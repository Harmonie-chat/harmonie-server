using Dapper;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Notifications;

public sealed class NotificationCleanupRepository : INotificationCleanupRepository
{
    private readonly DbSession _dbSession;

    public NotificationCleanupRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<int> DeleteOutboxBatchAsync(
        string status,
        DateTime processedBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return 0;

        const string sql = """
                           WITH deletable AS (
                               SELECT id
                               FROM message_notification_outbox
                               WHERE status = @Status
                                 AND processed_at_utc < @ProcessedBeforeUtc
                               ORDER BY processed_at_utc ASC, id ASC
                               LIMIT @BatchSize
                               FOR UPDATE SKIP LOCKED
                           )
                           DELETE FROM message_notification_outbox outbox
                           USING deletable
                           WHERE outbox.id = deletable.id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Status = status,
                ProcessedBeforeUtc = processedBeforeUtc,
                BatchSize = batchSize
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteAsync(command);
    }

    public async Task<int> DeleteExpiredDevicesBatchAsync(
        DateTime expiresBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return 0;

        const string sql = """
                           WITH deletable AS (
                               SELECT id
                               FROM notification_devices
                               WHERE expires_at_utc < @ExpiresBeforeUtc
                               ORDER BY expires_at_utc ASC, id ASC
                               LIMIT @BatchSize
                               FOR UPDATE SKIP LOCKED
                           )
                           DELETE FROM notification_devices devices
                           USING deletable
                           WHERE devices.id = deletable.id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                ExpiresBeforeUtc = expiresBeforeUtc,
                BatchSize = batchSize
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteAsync(command);
    }
}
