using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Harmonie.Workers.Tests.Notifications;

public sealed class PushNotificationBatchProcessorTests
{
    [Fact]
    public async Task ProcessBatchAsync_ShouldClaimAndDispatchJobs()
    {
        var jobs = new[]
        {
            CreateJob(),
            CreateJob()
        };
        var outboxRepositoryMock = CreateOutboxRepository(jobs);
        var dispatchService = new RecordingNotificationDispatchService();
        using var serviceProvider = BuildServiceProvider(dispatchService);
        var processor = CreateProcessor(
            serviceProvider,
            outboxRepositoryMock.Object,
            new PushNotificationOptions { BatchSize = 10, LockDurationSeconds = 60, MaxConcurrentJobs = 2 });

        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

        dispatchService.DispatchedJobIds.Should().BeEquivalentTo(jobs.Select(job => job.Id));
        outboxRepositoryMock.Verify(
            x => x.ClaimPendingAsync(
                10,
                It.IsAny<DateTime>(),
                TimeSpan.FromSeconds(60),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldRespectMaxConcurrentJobs()
    {
        var jobs = Enumerable.Range(0, 8).Select(_ => CreateJob()).ToArray();
        var outboxRepositoryMock = CreateOutboxRepository(jobs);
        var dispatchService = new RecordingNotificationDispatchService(TimeSpan.FromMilliseconds(50));
        using var serviceProvider = BuildServiceProvider(dispatchService);
        var processor = CreateProcessor(
            serviceProvider,
            outboxRepositoryMock.Object,
            new PushNotificationOptions { BatchSize = 10, LockDurationSeconds = 60, MaxConcurrentJobs = 2 });

        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

        dispatchService.MaxObservedConcurrency.Should().BeLessThanOrEqualTo(2);
        dispatchService.MaxObservedConcurrency.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenNoJobsClaimed_ShouldNotResolveDispatchService()
    {
        var outboxRepositoryMock = CreateOutboxRepository([]);
        using var serviceProvider = BuildServiceProvider(new ThrowingNotificationDispatchService());
        var processor = CreateProcessor(
            serviceProvider,
            outboxRepositoryMock.Object,
            new PushNotificationOptions { BatchSize = 10, LockDurationSeconds = 60, MaxConcurrentJobs = 2 });

        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
    }

    private static PushNotificationBatchProcessor CreateProcessor(
        ServiceProvider serviceProvider,
        IMessageNotificationOutboxRepository outboxRepository,
        PushNotificationOptions options)
    {
        return new PushNotificationBatchProcessor(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            outboxRepository,
            Options.Create(options),
            TestClock.Provider,
            NullLogger<PushNotificationBatchProcessor>.Instance);
    }

    private static ServiceProvider BuildServiceProvider(INotificationDispatchService dispatchService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => dispatchService);
        return services.BuildServiceProvider();
    }

    private static Mock<IMessageNotificationOutboxRepository> CreateOutboxRepository(
        IReadOnlyList<MessageNotificationOutboxJob> jobs)
    {
        var mock = new Mock<IMessageNotificationOutboxRepository>();
        mock.Setup(x => x.ClaimPendingAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);
        return mock;
    }

    private static MessageNotificationOutboxJob CreateJob()
    {
        return new MessageNotificationOutboxJob(
            Guid.NewGuid(),
            MessageId.New(),
            MessageNotificationOutboxStatuses.Processing,
            1,
            TestClock.UtcNow,
            TestClock.UtcNow.AddMinutes(5),
            null,
            TestClock.UtcNow,
            null);
    }

    private sealed class RecordingNotificationDispatchService : INotificationDispatchService
    {
        private readonly TimeSpan _delay;
        private readonly List<Guid> _dispatchedJobIds = new();
        private readonly Lock _lock = new();
        private int _currentConcurrency;
        private int _maxObservedConcurrency;

        public RecordingNotificationDispatchService(TimeSpan delay = default)
        {
            _delay = delay;
        }

        public IReadOnlyList<Guid> DispatchedJobIds
        {
            get
            {
                lock (_lock)
                {
                    return _dispatchedJobIds.ToArray();
                }
            }
        }

        public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);

        public async Task DispatchAsync(
            MessageNotificationOutboxJob job,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var concurrency = Interlocked.Increment(ref _currentConcurrency);
            TrackMaxConcurrency(concurrency);

            try
            {
                lock (_lock)
                {
                    _dispatchedJobIds.Add(job.Id);
                }

                if (_delay > TimeSpan.Zero)
                    await Task.Delay(_delay, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        private void TrackMaxConcurrency(int concurrency)
        {
            while (true)
            {
                var currentMax = Volatile.Read(ref _maxObservedConcurrency);
                if (concurrency <= currentMax)
                    return;

                if (Interlocked.CompareExchange(ref _maxObservedConcurrency, concurrency, currentMax) == currentMax)
                    return;
            }
        }
    }

    private sealed class ThrowingNotificationDispatchService : INotificationDispatchService
    {
        public Task DispatchAsync(
            MessageNotificationOutboxJob job,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Dispatch service should not be resolved when no jobs are claimed.");
        }
    }
}
