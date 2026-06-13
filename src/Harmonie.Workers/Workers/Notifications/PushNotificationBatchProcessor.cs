using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Workers.Workers.Notifications;

public sealed class PushNotificationBatchProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageNotificationOutboxRepository _outboxRepository;
    private readonly PushNotificationOptions _options;
    private readonly ILogger<PushNotificationBatchProcessor> _logger;

    public PushNotificationBatchProcessor(
        IServiceScopeFactory scopeFactory,
        IMessageNotificationOutboxRepository outboxRepository,
        IOptions<PushNotificationOptions> options,
        ILogger<PushNotificationBatchProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _outboxRepository = outboxRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var jobs = await _outboxRepository.ClaimPendingAsync(
            _options.BatchSize,
            nowUtc,
            TimeSpan.FromSeconds(_options.LockDurationSeconds),
            cancellationToken);

        if (jobs.Count == 0)
            return;

        _logger.LogInformation(
            "Claimed {JobCount} message notification outbox jobs.",
            jobs.Count);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxConcurrentJobs,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(jobs, parallelOptions, async (job, ct) =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dispatchService = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();
            await dispatchService.DispatchAsync(job, DateTime.UtcNow, ct);
        });
    }
}
