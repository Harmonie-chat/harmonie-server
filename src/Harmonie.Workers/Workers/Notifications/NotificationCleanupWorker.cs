using Harmonie.Application.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Workers.Workers.Notifications;

public sealed class NotificationCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationCleanupOptions _options;
    private readonly ILogger<NotificationCleanupWorker> _logger;

    public NotificationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationCleanupOptions> options,
        ILogger<NotificationCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notification cleanup worker is disabled.");
            return;
        }

        _logger.LogInformation("Notification cleanup worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<INotificationCleanupProcessor>();
            await processor.ProcessAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification cleanup worker run failed.");
        }
    }
}
