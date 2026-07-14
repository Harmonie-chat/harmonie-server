using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Workers.Workers.Notifications;

public sealed class PushNotificationBatchProcessor : IPushNotificationBatchProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageNotificationOutboxRepository _outboxRepository;
    private readonly PushNotificationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PushNotificationBatchProcessor> _logger;

    public PushNotificationBatchProcessor(
        IServiceScopeFactory scopeFactory,
        IMessageNotificationOutboxRepository outboxRepository,
        IOptions<PushNotificationOptions> options,
        TimeProvider timeProvider,
        ILogger<PushNotificationBatchProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _outboxRepository = outboxRepository;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
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
            var dispatchService = scope.ServiceProvider.GetRequiredService<INotificationDispatchService>();
            await dispatchService.DispatchAsync(job, _timeProvider.GetUtcNow().UtcDateTime, ct);
        });
    }
}
