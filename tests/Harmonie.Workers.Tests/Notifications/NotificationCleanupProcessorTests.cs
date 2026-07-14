using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Harmonie.Workers.Tests.Notifications;

public sealed class NotificationCleanupProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ShouldUseConfiguredRetentionCutoffs()
    {
        var cleanupRepositoryMock = new Mock<INotificationCleanupRepository>();
        var outboxCalls = new List<(string Status, DateTime CutoffUtc, int BatchSize)>();
        var deviceCalls = new List<(DateTime CutoffUtc, int BatchSize)>();

        cleanupRepositoryMock
            .Setup(repository => repository.DeleteOutboxBatchAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, int, CancellationToken>((status, cutoffUtc, batchSize, _) =>
                outboxCalls.Add((status, cutoffUtc, batchSize)))
            .ReturnsAsync(0);
        cleanupRepositoryMock
            .Setup(repository => repository.DeleteExpiredDevicesBatchAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((cutoffUtc, batchSize, _) =>
                deviceCalls.Add((cutoffUtc, batchSize)))
            .ReturnsAsync(0);

        var processor = CreateProcessor(
            cleanupRepositoryMock.Object,
            new NotificationCleanupOptions
            {
                BatchSize = 250,
                ProcessedOutboxRetentionDays = 8,
                FailedOutboxRetentionDays = 31,
                ExpiredDeviceRetentionDays = 3
            });
        var beforeProcessingUtc = DateTime.UtcNow;

        await processor.ProcessAsync(TestContext.Current.CancellationToken);

        var processedCall = outboxCalls.Should()
            .ContainSingle(call => call.Status == MessageNotificationOutboxStatuses.Processed)
            .Which;
        processedCall.BatchSize.Should().Be(250);
        processedCall.CutoffUtc.Should().BeCloseTo(beforeProcessingUtc.AddDays(-8), TimeSpan.FromSeconds(1));

        var failedCall = outboxCalls.Should()
            .ContainSingle(call => call.Status == MessageNotificationOutboxStatuses.Failed)
            .Which;
        failedCall.BatchSize.Should().Be(250);
        failedCall.CutoffUtc.Should().BeCloseTo(beforeProcessingUtc.AddDays(-31), TimeSpan.FromSeconds(1));

        var deviceCall = deviceCalls.Should().ContainSingle().Which;
        deviceCall.BatchSize.Should().Be(250);
        deviceCall.CutoffUtc.Should().BeCloseTo(beforeProcessingUtc.AddDays(-3), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ProcessAsync_WhenRowsExceedBatchSize_ShouldContinueUntilEachTableIsEmpty()
    {
        var cleanupRepositoryMock = new Mock<INotificationCleanupRepository>();
        cleanupRepositoryMock
            .SetupSequence(repository => repository.DeleteOutboxBatchAsync(
                MessageNotificationOutboxStatuses.Processed,
                It.IsAny<DateTime>(),
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2)
            .ReturnsAsync(1)
            .ReturnsAsync(0);
        cleanupRepositoryMock
            .SetupSequence(repository => repository.DeleteOutboxBatchAsync(
                MessageNotificationOutboxStatuses.Failed,
                It.IsAny<DateTime>(),
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2)
            .ReturnsAsync(0);
        cleanupRepositoryMock
            .SetupSequence(repository => repository.DeleteExpiredDevicesBatchAsync(
                It.IsAny<DateTime>(),
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2)
            .ReturnsAsync(1)
            .ReturnsAsync(0);
        var processor = CreateProcessor(cleanupRepositoryMock.Object, new NotificationCleanupOptions { BatchSize = 2 });

        await processor.ProcessAsync(TestContext.Current.CancellationToken);

        cleanupRepositoryMock.Verify(
            repository => repository.DeleteOutboxBatchAsync(
                MessageNotificationOutboxStatuses.Processed,
                It.IsAny<DateTime>(),
                2,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        cleanupRepositoryMock.Verify(
            repository => repository.DeleteOutboxBatchAsync(
                MessageNotificationOutboxStatuses.Failed,
                It.IsAny<DateTime>(),
                2,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        cleanupRepositoryMock.Verify(
            repository => repository.DeleteExpiredDevicesBatchAsync(
                It.IsAny<DateTime>(),
                2,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessAsync_WhenNoRowsAreEligible_ShouldPerformOneNoOpBatchPerCleanupTarget()
    {
        var cleanupRepositoryMock = new Mock<INotificationCleanupRepository>();
        cleanupRepositoryMock
            .Setup(repository => repository.DeleteOutboxBatchAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        cleanupRepositoryMock
            .Setup(repository => repository.DeleteExpiredDevicesBatchAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var processor = CreateProcessor(cleanupRepositoryMock.Object, new NotificationCleanupOptions());

        await processor.ProcessAsync(TestContext.Current.CancellationToken);

        cleanupRepositoryMock.Verify(
            repository => repository.DeleteOutboxBatchAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        cleanupRepositoryMock.Verify(
            repository => repository.DeleteExpiredDevicesBatchAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static NotificationCleanupProcessor CreateProcessor(
        INotificationCleanupRepository cleanupRepository,
        NotificationCleanupOptions options)
    {
        return new NotificationCleanupProcessor(
            cleanupRepository,
            Options.Create(options),
            NullLogger<NotificationCleanupProcessor>.Instance);
    }
}
