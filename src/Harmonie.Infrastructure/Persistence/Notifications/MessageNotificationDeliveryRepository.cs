using Dapper;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Notifications;

public sealed class MessageNotificationDeliveryRepository : IMessageNotificationDeliveryRepository
{
    private readonly DbSession _dbSession;

    public MessageNotificationDeliveryRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task EnsurePendingAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
            return;

        const string sql = """
                           INSERT INTO message_notification_deliveries (
                               id,
                               outbox_job_id,
                               device_id,
                               status,
                               attempts,
                               last_error,
                               first_attempted_at_utc,
                               last_attempted_at_utc,
                               succeeded_at_utc,
                               created_at_utc,
                               updated_at_utc)
                           VALUES (
                               @Id,
                               @OutboxJobId,
                               @DeviceId,
                               @Status,
                               0,
                               NULL,
                               NULL,
                               NULL,
                               NULL,
                               @NowUtc,
                               @NowUtc)
                           ON CONFLICT (outbox_job_id, device_id) DO NOTHING
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        foreach (var deviceId in deviceIds.Distinct())
        {
            var command = new CommandDefinition(
                sql,
                new
                {
                    Id = Guid.NewGuid(),
                    OutboxJobId = outboxJobId,
                    DeviceId = deviceId,
                    Status = MessageNotificationDeliveryStatuses.Pending,
                    NowUtc = createdAtUtc
                },
                transaction: _dbSession.Transaction,
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
    }

    public async Task<IReadOnlyList<MessageNotificationDelivery>> GetByJobAndDeviceIdsAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
            return [];

        const string sql = """
                           SELECT
                               id AS "Id",
                               outbox_job_id AS "OutboxJobId",
                               device_id AS "DeviceId",
                               status AS "Status",
                               attempts AS "Attempts",
                               last_error AS "LastError",
                               first_attempted_at_utc AS "FirstAttemptedAtUtc",
                               last_attempted_at_utc AS "LastAttemptedAtUtc",
                               succeeded_at_utc AS "SucceededAtUtc",
                               created_at_utc AS "CreatedAtUtc",
                               updated_at_utc AS "UpdatedAtUtc"
                           FROM message_notification_deliveries
                           WHERE outbox_job_id = @OutboxJobId
                             AND device_id = ANY(@DeviceIds)
                           ORDER BY created_at_utc ASC, id ASC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                OutboxJobId = outboxJobId,
                DeviceIds = deviceIds.Distinct().ToArray()
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<MessageNotificationDeliveryRow>(command);
        return rows
            .Select(row => new MessageNotificationDelivery(
                row.Id,
                row.OutboxJobId,
                row.DeviceId,
                row.Status,
                row.Attempts,
                row.LastError,
                row.FirstAttemptedAtUtc,
                row.LastAttemptedAtUtc,
                row.SucceededAtUtc,
                row.CreatedAtUtc,
                row.UpdatedAtUtc))
            .ToArray();
    }

    public async Task MarkResultsAsync(
        Guid outboxJobId,
        IReadOnlyCollection<NotificationDeliveryResult> results,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
            return;

        const string sql = """
                           UPDATE message_notification_deliveries
                           SET status = @Status,
                               attempts = attempts + 1,
                               last_error = @Error,
                               first_attempted_at_utc = COALESCE(first_attempted_at_utc, @AttemptedAtUtc),
                               last_attempted_at_utc = @AttemptedAtUtc,
                               succeeded_at_utc = CASE
                                   WHEN @Status = @SucceededStatus THEN @AttemptedAtUtc
                                   ELSE succeeded_at_utc
                               END,
                               updated_at_utc = @AttemptedAtUtc
                           WHERE outbox_job_id = @OutboxJobId
                             AND device_id = @DeviceId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        foreach (var result in results)
        {
            var command = new CommandDefinition(
                sql,
                new
                {
                    OutboxJobId = outboxJobId,
                    result.DeviceId,
                    Status = ToDeliveryStatus(result.Status),
                    result.Error,
                    AttemptedAtUtc = attemptedAtUtc,
                    SucceededStatus = MessageNotificationDeliveryStatuses.Succeeded
                },
                transaction: _dbSession.Transaction,
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
    }

    public async Task MarkDevicesFailedAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        string error,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
            return;

        const string sql = """
                           UPDATE message_notification_deliveries
                           SET status = @Status,
                               last_error = @Error,
                               updated_at_utc = @FailedAtUtc
                           WHERE outbox_job_id = @OutboxJobId
                             AND device_id = ANY(@DeviceIds)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                OutboxJobId = outboxJobId,
                DeviceIds = deviceIds.Distinct().ToArray(),
                Status = MessageNotificationDeliveryStatuses.Failed,
                Error = error,
                FailedAtUtc = failedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private static string ToDeliveryStatus(NotificationDeliveryResultStatus status)
    {
        return status switch
        {
            NotificationDeliveryResultStatus.Succeeded => MessageNotificationDeliveryStatuses.Succeeded,
            NotificationDeliveryResultStatus.InvalidDevice => MessageNotificationDeliveryStatuses.InvalidDevice,
            NotificationDeliveryResultStatus.TransientFailure => MessageNotificationDeliveryStatuses.TransientFailure,
            NotificationDeliveryResultStatus.PermanentFailure => MessageNotificationDeliveryStatuses.Failed,
            _ => MessageNotificationDeliveryStatuses.Failed
        };
    }

    private sealed class MessageNotificationDeliveryRow
    {
        public Guid Id { get; init; }

        public Guid OutboxJobId { get; init; }

        public Guid DeviceId { get; init; }

        public string Status { get; init; } = string.Empty;

        public int Attempts { get; init; }

        public string? LastError { get; init; }

        public DateTime? FirstAttemptedAtUtc { get; init; }

        public DateTime? LastAttemptedAtUtc { get; init; }

        public DateTime? SucceededAtUtc { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; init; }
    }
}
