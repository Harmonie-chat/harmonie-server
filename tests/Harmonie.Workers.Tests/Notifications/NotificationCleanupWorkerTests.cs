using FluentAssertions;
using Harmonie.Application.Services.Notifications;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Workers.Tests.Notifications;

public sealed class NotificationCleanupWorkerTests
{
    [Fact]
    public async Task StartAsync_WhenDisabled_ShouldNotCreateScope()
    {
        var worker = new NotificationCleanupWorker(
            new ThrowingServiceScopeFactory(),
            Options.Create(new NotificationCleanupOptions { Enabled = false }),
            NullLogger<NotificationCleanupWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenEnabled_ShouldProcessAtLeastOnce()
    {
        var processor = new RecordingNotificationCleanupProcessor();
        var services = new ServiceCollection();
        services.AddScoped<INotificationCleanupProcessor>(_ => processor);
        await using var serviceProvider = services.BuildServiceProvider();
        var worker = new NotificationCleanupWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new NotificationCleanupOptions
            {
                Enabled = true,
                PollIntervalSeconds = 86_400,
                BatchSize = 1,
                ProcessedOutboxRetentionDays = 1,
                FailedOutboxRetentionDays = 1,
                ExpiredDeviceRetentionDays = 0
            }),
            NullLogger<NotificationCleanupWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await processor.WaitForFirstRunAsync(TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        processor.RunCount.Should().BeGreaterThanOrEqualTo(1);
    }

    private sealed class RecordingNotificationCleanupProcessor : INotificationCleanupProcessor
    {
        private readonly TaskCompletionSource _firstRun = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _runCount;

        public int RunCount => Volatile.Read(ref _runCount);

        public Task ProcessAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _runCount);
            _firstRun.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task WaitForFirstRunAsync(CancellationToken cancellationToken)
        {
            await _firstRun.Task.WaitAsync(cancellationToken);
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
