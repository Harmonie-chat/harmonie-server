using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harmonie.Workers.Workers.Notifications;

public sealed class PushNotificationWorker : BackgroundService
{
    private readonly ILogger<PushNotificationWorker> _logger;

    public PushNotificationWorker(ILogger<PushNotificationWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Push notification worker started. Dispatch is not implemented yet.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
    }
}
