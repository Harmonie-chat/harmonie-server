namespace Harmonie.Workers.Workers.Notifications;

public interface IPushNotificationBatchProcessor
{
    Task ProcessBatchAsync(CancellationToken cancellationToken = default);
}
