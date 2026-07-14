namespace Harmonie.Workers.Workers.Notifications;

public interface INotificationCleanupProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken = default);
}
