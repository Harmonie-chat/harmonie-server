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

    public async Task<IReadOnlySet<Guid>> GetOrCreateRetryableDeviceIdsAsync(
        Guid outboxJobId,
        IReadOnlyCollection<Guid> deviceIds,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
            return new HashSet<Guid>();

        const string sql = """
                           WITH input AS (
                               SELECT *
                               FROM unnest(@DeviceIds::uuid[], @DeliveryIds::uuid[]) AS input(device_id, delivery_id)
                           ), existing AS (
                               SELECT deliveries.device_id
                               FROM message_notification_deliveries deliveries
                               JOIN input ON input.device_id = deliveries.device_id
                               WHERE deliveries.outbox_job_id = @OutboxJobId
                                 AND deliveries.status = ANY(@RetryableStatuses)
                           ), inserted AS (
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
                               SELECT
                                   input.delivery_id,
                                   @OutboxJobId,
                                   input.device_id,
                                   @PendingStatus,
                                   0,
                                   NULL,
                                   NULL,
                                   NULL,
                                   NULL,
                                   @NowUtc,
                                   @NowUtc
                               FROM input
                               ON CONFLICT (outbox_job_id, device_id) DO NOTHING
                               RETURNING device_id
                           )
                           SELECT device_id FROM existing
                           UNION
                           SELECT device_id FROM inserted
                           """;

        var distinctDeviceIds = deviceIds.Distinct().ToArray();
        var deliveryIds = distinctDeviceIds.Select(_ => Guid.NewGuid()).ToArray();
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                OutboxJobId = outboxJobId,
                DeviceIds = distinctDeviceIds,
                DeliveryIds = deliveryIds,
                PendingStatus = MessageNotificationDeliveryStatuses.Pending,
                RetryableStatuses = new[]
                {
                    MessageNotificationDeliveryStatuses.Pending,
                    MessageNotificationDeliveryStatuses.TransientFailure
                },
                NowUtc = createdAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var retryableDeviceIds = await connection.QueryAsync<Guid>(command);
        return retryableDeviceIds.ToHashSet();
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
                           WITH results AS (
                               SELECT *
                               FROM unnest(@DeviceIds::uuid[], @Statuses::text[], @Errors::text[])
                                   AS results(device_id, status, error)
                           )
                           UPDATE message_notification_deliveries deliveries
                           SET status = results.status,
                               attempts = deliveries.attempts + 1,
                               last_error = results.error,
                               first_attempted_at_utc = COALESCE(deliveries.first_attempted_at_utc, @AttemptedAtUtc),
                               last_attempted_at_utc = @AttemptedAtUtc,
                               succeeded_at_utc = CASE
                                   WHEN results.status = @SucceededStatus THEN @AttemptedAtUtc
                                   ELSE deliveries.succeeded_at_utc
                               END,
                               updated_at_utc = @AttemptedAtUtc
                           FROM results
                           WHERE deliveries.outbox_job_id = @OutboxJobId
                             AND deliveries.device_id = results.device_id
                           """;

        var resultArray = results.ToArray();
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                OutboxJobId = outboxJobId,
                DeviceIds = resultArray.Select(result => result.DeviceId).ToArray(),
                Statuses = resultArray.Select(result => ToDeliveryStatus(result.Status)).ToArray(),
                Errors = resultArray.Select(result => result.Error).ToArray(),
                AttemptedAtUtc = attemptedAtUtc,
                SucceededStatus = MessageNotificationDeliveryStatuses.Succeeded
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
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
}
