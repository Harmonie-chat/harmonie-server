using Harmonie.Application.Interfaces.Notifications;

namespace Harmonie.Application.Services.Notifications;

public interface INotificationDispatchService
{
    Task DispatchAsync(
        MessageNotificationOutboxJob job,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
