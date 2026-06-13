using FluentAssertions;
using Harmonie.Application.Services.Notifications;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Workers.Tests.Notifications;

public sealed class PushNotificationWorkerTests
{
    [Fact]
    public async Task StartAsync_WhenDisabled_ShouldNotCreateScope()
    {
        var worker = new PushNotificationWorker(
            new ThrowingServiceScopeFactory(),
            Options.Create(new PushNotificationOptions { Enabled = false }),
            NullLogger<PushNotificationWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenEnabled_ShouldProcessAtLeastOneBatch()
    {
        var processor = new RecordingPushNotificationBatchProcessor();
        var services = new ServiceCollection();
        services.AddScoped<IPushNotificationBatchProcessor>(_ => processor);
        await using var serviceProvider = services.BuildServiceProvider();
        var worker = new PushNotificationWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PushNotificationOptions
            {
                Enabled = true,
                PollIntervalSeconds = 3600,
                BatchSize = 1,
                LockDurationSeconds = 60,
                MaxConcurrentJobs = 1
            }),
            NullLogger<PushNotificationWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await processor.WaitForFirstBatchAsync(TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        processor.ProcessedBatchCount.Should().BeGreaterThanOrEqualTo(1);
    }

    private sealed class RecordingPushNotificationBatchProcessor : IPushNotificationBatchProcessor
    {
        private readonly TaskCompletionSource _firstBatchProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _processedBatchCount;

        public int ProcessedBatchCount => Volatile.Read(ref _processedBatchCount);

        public Task ProcessBatchAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _processedBatchCount);
            _firstBatchProcessed.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task WaitForFirstBatchAsync(CancellationToken cancellationToken)
        {
            await _firstBatchProcessed.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ThrowingServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            throw new InvalidOperationException("Disabled worker should not create a scope.");
        }
    }
}
