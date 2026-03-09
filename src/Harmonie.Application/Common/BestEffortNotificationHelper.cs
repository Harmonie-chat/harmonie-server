using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Common;

public static class BestEffortNotificationHelper
{
    public static async Task TryNotifyAsync(
        Func<CancellationToken, Task> notificationAction,
        TimeSpan timeout,
        ILogger logger,
        string failureMessage,
        params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(notificationAction);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        try
        {
            using var notificationCts = new CancellationTokenSource(timeout);
            await notificationAction(notificationCts.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, failureMessage, args);
        }
    }
}
