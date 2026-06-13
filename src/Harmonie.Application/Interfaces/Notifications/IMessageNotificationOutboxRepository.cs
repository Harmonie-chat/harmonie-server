using Harmonie.Domain.ValueObjects.Messages;

namespace Harmonie.Application.Interfaces.Notifications;

public static class MessageNotificationOutboxStatuses
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Processed = "processed";
    public const string Failed = "failed";
}

public sealed record MessageNotificationOutboxJob(
    Guid Id,
    MessageId MessageId,
    string Status,
    int Attempts,
    DateTime NextAttemptAtUtc,
    DateTime? LockedUntilUtc,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public interface IMessageNotificationOutboxRepository
{
    Task AddPendingAsync(
        MessageId messageId,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageNotificationOutboxJob>> ClaimPendingAsync(
        int batchSize,
        DateTime nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        Guid jobId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid jobId,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid jobId,
        string error,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default);
}
