using Dapper;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Notifications;

public sealed class MessageNotificationOutboxRepository : IMessageNotificationOutboxRepository
{
    private readonly DbSession _dbSession;
    private readonly TimeProvider _timeProvider;

    public MessageNotificationOutboxRepository(DbSession dbSession, TimeProvider timeProvider)
    {
        _dbSession = dbSession;
        _timeProvider = timeProvider;
    }

    public async Task AddPendingAsync(
        MessageId messageId,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO message_notification_outbox (
                               id,
                               message_id,
                               status,
                               attempts,
                               next_attempt_at_utc,
                               locked_until_utc,
                               last_error,
                               created_at_utc,
                               processed_at_utc)
                           VALUES (
                               @Id,
                               @MessageId,
                               @Status,
                               0,
                               @NextAttemptAtUtc,
                               NULL,
                               NULL,
                               @CreatedAtUtc,
                               NULL)
                           """;

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                MessageId = messageId.Value,
                Status = MessageNotificationOutboxStatuses.Pending,
                NextAttemptAtUtc = nextAttemptAtUtc,
                CreatedAtUtc = nowUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<MessageNotificationOutboxJob>> ClaimPendingAsync(
        int batchSize,
        DateTime nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return [];

        const string sql = """
                           WITH claimable AS (
                               SELECT id
                               FROM message_notification_outbox
                               WHERE (
                                       status = @PendingStatus
                                       AND next_attempt_at_utc <= @NowUtc
                                   )
                                   OR (
                                       status = @ProcessingStatus
                                       AND locked_until_utc IS NOT NULL
                                       AND locked_until_utc <= @NowUtc
                                   )
                               ORDER BY next_attempt_at_utc ASC, created_at_utc ASC, id ASC
                               LIMIT @BatchSize
                               FOR UPDATE SKIP LOCKED
                           )
                           UPDATE message_notification_outbox outbox
                           SET status = @ProcessingStatus,
                               attempts = outbox.attempts + 1,
                               locked_until_utc = @LockedUntilUtc,
                               last_error = NULL
                           FROM claimable
                           WHERE outbox.id = claimable.id
                           RETURNING
                               outbox.id AS "Id",
                               outbox.message_id AS "MessageId",
                               outbox.status AS "Status",
                               outbox.attempts AS "Attempts",
                               outbox.next_attempt_at_utc AS "NextAttemptAtUtc",
                               outbox.locked_until_utc AS "LockedUntilUtc",
                               outbox.last_error AS "LastError",
                               outbox.created_at_utc AS "CreatedAtUtc",
                               outbox.processed_at_utc AS "ProcessedAtUtc"
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                PendingStatus = MessageNotificationOutboxStatuses.Pending,
                ProcessingStatus = MessageNotificationOutboxStatuses.Processing,
                NowUtc = nowUtc,
                LockedUntilUtc = nowUtc.Add(lockDuration),
                BatchSize = batchSize
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<MessageNotificationOutboxJobRow>(command);
        return rows
            .Select(row => new MessageNotificationOutboxJob(
                row.Id,
                MessageId.From(row.MessageId),
                row.Status,
                row.Attempts,
                row.NextAttemptAtUtc,
                row.LockedUntilUtc,
                row.LastError,
                row.CreatedAtUtc,
                row.ProcessedAtUtc))
            .ToArray();
    }

    public async Task MarkProcessedAsync(
        Guid jobId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE message_notification_outbox
                           SET status = @Status,
                               locked_until_utc = NULL,
                               last_error = NULL,
                               processed_at_utc = @ProcessedAtUtc
                           WHERE id = @JobId
                           """;

        await ExecuteStateChangeAsync(
            sql,
            new
            {
                JobId = jobId,
                Status = MessageNotificationOutboxStatuses.Processed,
                ProcessedAtUtc = processedAtUtc
            },
            cancellationToken);
    }

    public async Task ScheduleRetryAsync(
        Guid jobId,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE message_notification_outbox
                           SET status = @Status,
                               next_attempt_at_utc = @NextAttemptAtUtc,
                               locked_until_utc = NULL,
                               last_error = @Error
                           WHERE id = @JobId
                           """;

        await ExecuteStateChangeAsync(
            sql,
            new
            {
                JobId = jobId,
                Status = MessageNotificationOutboxStatuses.Pending,
                NextAttemptAtUtc = nextAttemptAtUtc,
                Error = error
            },
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid jobId,
        string error,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE message_notification_outbox
                           SET status = @Status,
                               locked_until_utc = NULL,
                               last_error = @Error,
                               processed_at_utc = @FailedAtUtc
                           WHERE id = @JobId
                           """;

        await ExecuteStateChangeAsync(
            sql,
            new
            {
                JobId = jobId,
                Status = MessageNotificationOutboxStatuses.Failed,
                Error = error,
                FailedAtUtc = failedAtUtc
            },
            cancellationToken);
    }

    private async Task ExecuteStateChangeAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private sealed class MessageNotificationOutboxJobRow
    {
        public Guid Id { get; init; }

        public Guid MessageId { get; init; }

        public string Status { get; init; } = string.Empty;

        public int Attempts { get; init; }

        public DateTime NextAttemptAtUtc { get; init; }

        public DateTime? LockedUntilUtc { get; init; }

        public string? LastError { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime? ProcessedAtUtc { get; init; }
    }
}
