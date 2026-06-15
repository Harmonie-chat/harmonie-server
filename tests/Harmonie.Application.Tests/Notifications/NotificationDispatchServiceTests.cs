using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class NotificationDispatchServiceTests
{
    private readonly Mock<IMessageNotificationContextRepository> _contextRepositoryMock = new();
    private readonly Mock<INotificationDeviceRepository> _deviceRepositoryMock = new();
    private readonly Mock<IMessageNotificationOutboxRepository> _outboxRepositoryMock = new();
    private readonly InMemoryMessageNotificationDeliveryRepository _deliveryRepository = new();

    [Fact]
    public async Task DispatchAsync_ShouldSendOnlyThroughMatchingPlatformAdapter()
    {
        var recipientId = UserId.New();
        var context = CreateContext(recipientId);
        var webPushDevice = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var fcmDevice = CreateDevice(recipientId, "android_fcm");
        var webPushAdapter = new StubNotificationDeliveryAdapter(
            NotificationDevicePlatforms.WebPush,
            device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.Succeeded));
        var fcmAdapter = new StubNotificationDeliveryAdapter(
            "android_fcm",
            device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.Succeeded));
        var service = CreateService(webPushAdapter, fcmAdapter);

        SetupContextAndDevices(context, webPushDevice, fcmDevice);

        await service.DispatchAsync(CreateJob(attempts: 1), DateTime.UtcNow, TestContext.Current.CancellationToken);

        webPushAdapter.SentDeviceIds.Should().ContainSingle().Which.Should().Be(webPushDevice.Id);
        fcmAdapter.SentDeviceIds.Should().ContainSingle().Which.Should().Be(fcmDevice.Id);
        _outboxRepositoryMock.Verify(
            x => x.MarkProcessedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _outboxRepositoryMock.Verify(
            x => x.ScheduleRetryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenWebPushDeviceIsInvalid_ShouldDeleteDeviceAndMarkProcessed()
    {
        var recipientId = UserId.New();
        var invalidDevice = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var adapter = new StubNotificationDeliveryAdapter(
            NotificationDevicePlatforms.WebPush,
            device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.InvalidDevice, "gone"));
        var service = CreateService(adapter);

        SetupContextAndDevices(CreateContext(recipientId), invalidDevice);

        await service.DispatchAsync(CreateJob(attempts: 1), DateTime.UtcNow, TestContext.Current.CancellationToken);

        _deviceRepositoryMock.Verify(
            x => x.DeleteManyAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(invalidDevice.Id)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _outboxRepositoryMock.Verify(
            x => x.MarkProcessedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenTransientFailureBeforeMaxAttempts_ShouldScheduleRetry()
    {
        var recipientId = UserId.New();
        var device = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var nowUtc = new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);
        var adapter = new StubNotificationDeliveryAdapter(
            NotificationDevicePlatforms.WebPush,
            device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.TransientFailure, "timeout"));
        var service = CreateService(adapter);

        SetupContextAndDevices(CreateContext(recipientId), device);

        await service.DispatchAsync(CreateJob(attempts: 2), nowUtc, TestContext.Current.CancellationToken);

        _outboxRepositoryMock.Verify(
            x => x.ScheduleRetryAsync(
                It.IsAny<Guid>(),
                nowUtc.AddSeconds(60),
                It.Is<string>(error => error.Contains("timeout", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _outboxRepositoryMock.Verify(
            x => x.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenTransientFailureReachesMaxAttempts_ShouldMarkFailed()
    {
        var recipientId = UserId.New();
        var device = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var nowUtc = new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);
        var adapter = new StubNotificationDeliveryAdapter(
            NotificationDevicePlatforms.WebPush,
            device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.TransientFailure, "timeout"));
        var service = CreateService(adapter);

        SetupContextAndDevices(CreateContext(recipientId), device);

        await service.DispatchAsync(CreateJob(attempts: 3), nowUtc, TestContext.Current.CancellationToken);

        _outboxRepositoryMock.Verify(
            x => x.MarkFailedAsync(
                It.IsAny<Guid>(),
                It.Is<string>(error => error.Contains("timeout", StringComparison.Ordinal)),
                nowUtc,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _outboxRepositoryMock.Verify(
            x => x.ScheduleRetryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenRetryingPartialFailure_ShouldNotResendToSucceededDevice()
    {
        var recipientId = UserId.New();
        var succeededDevice = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var retryingDevice = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var jobId = Guid.NewGuid();
        var resultsByDeviceId = new Dictionary<Guid, NotificationDeliveryResultStatus>
        {
            [succeededDevice.Id] = NotificationDeliveryResultStatus.Succeeded,
            [retryingDevice.Id] = NotificationDeliveryResultStatus.TransientFailure
        };
        var adapter = new StubNotificationDeliveryAdapter(
            NotificationDevicePlatforms.WebPush,
            device => new NotificationDeliveryResult(device.Id, resultsByDeviceId[device.Id], "timeout"));
        var service = CreateService(adapter);

        SetupContextAndDevices(CreateContext(recipientId), succeededDevice, retryingDevice);

        await service.DispatchAsync(CreateJob(jobId, attempts: 1), DateTime.UtcNow, TestContext.Current.CancellationToken);
        await service.DispatchAsync(CreateJob(jobId, attempts: 2), DateTime.UtcNow, TestContext.Current.CancellationToken);

        adapter.SentDeviceIds.Count(deviceId => deviceId == succeededDevice.Id).Should().Be(1);
        adapter.SentDeviceIds.Count(deviceId => deviceId == retryingDevice.Id).Should().Be(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenOnlyPermanentFailuresOccur_ShouldMarkProcessedWithoutRetry()
    {
        var recipientId = UserId.New();
        var device = CreateDevice(recipientId, NotificationDevicePlatforms.WebPush);
        var adapter = new StubNotificationDeliveryAdapter(
            NotificationDevicePlatforms.WebPush,
            device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.PermanentFailure, "forbidden"));
        var service = CreateService(adapter);

        SetupContextAndDevices(CreateContext(recipientId), device);

        await service.DispatchAsync(CreateJob(attempts: 1), DateTime.UtcNow, TestContext.Current.CancellationToken);

        _outboxRepositoryMock.Verify(
            x => x.MarkProcessedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _outboxRepositoryMock.Verify(
            x => x.ScheduleRetryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private NotificationDispatchService CreateService(params INotificationDeliveryAdapter[] adapters)
    {
        return new NotificationDispatchService(
            _contextRepositoryMock.Object,
            _deviceRepositoryMock.Object,
            _outboxRepositoryMock.Object,
            _deliveryRepository,
            new MessageNotificationPolicy(),
            new MessageNotificationPayloadFactory(),
            adapters,
            Options.Create(new PushNotificationOptions
            {
                MaxAttempts = 3,
                RetryBaseDelaySeconds = 30
            }),
            NullLogger<NotificationDispatchService>.Instance);
    }

    private void SetupContextAndDevices(
        MessageNotificationContext context,
        params NotificationDevice[] devices)
    {
        _contextRepositoryMock
            .Setup(x => x.GetAsync(It.IsAny<MessageId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _deviceRepositoryMock
            .Setup(x => x.GetActiveByUserIdsAsync(
                It.IsAny<IReadOnlyCollection<UserId>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);
        _outboxRepositoryMock
            .Setup(x => x.MarkProcessedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outboxRepositoryMock
            .Setup(x => x.ScheduleRetryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outboxRepositoryMock
            .Setup(x => x.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _deviceRepositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _deviceRepositoryMock
            .Setup(x => x.DeleteManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static MessageNotificationContext CreateContext(UserId recipientId)
    {
        var authorId = UserId.New();
        return new MessageNotificationContext(
            MessageId.New(),
            authorId,
            "alice",
            "Alice",
            new MessageNotificationTarget.Channel(GuildId.New(), "Harmonie", GuildChannelId.New(), "general"),
            new HashSet<UserId> { authorId, recipientId },
            new HashSet<UserId> { recipientId });
    }

    private static NotificationDevice CreateDevice(UserId userId, string platform)
    {
        return new NotificationDevice(
            Guid.NewGuid(),
            userId,
            platform,
            $"https://push.example.com/{Guid.NewGuid():N}",
            "p256dh",
            "auth",
            null);
    }

    private static MessageNotificationOutboxJob CreateJob(int attempts)
    {
        return CreateJob(Guid.NewGuid(), attempts);
    }

    private static MessageNotificationOutboxJob CreateJob(Guid jobId, int attempts)
    {
        return new MessageNotificationOutboxJob(
            jobId,
            MessageId.New(),
            MessageNotificationOutboxStatuses.Processing,
            attempts,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(5),
            null,
            DateTime.UtcNow,
            null);
    }

    private sealed class StubNotificationDeliveryAdapter : INotificationDeliveryAdapter
    {
        private readonly Func<NotificationDevice, NotificationDeliveryResult> _resultFactory;
        private readonly List<Guid> _sentDeviceIds = new();

        public StubNotificationDeliveryAdapter(
            string platform,
            Func<NotificationDevice, NotificationDeliveryResult> resultFactory)
        {
            Platform = platform;
            _resultFactory = resultFactory;
        }

        public string Platform { get; }

        public IReadOnlyList<Guid> SentDeviceIds => _sentDeviceIds;

        public Task<IReadOnlyList<NotificationDeliveryResult>> SendAsync(
            NotificationDeliveryPayload payload,
            IReadOnlyList<NotificationDevice> devices,
            CancellationToken cancellationToken = default)
        {
            _sentDeviceIds.AddRange(devices.Select(device => device.Id));
            return Task.FromResult<IReadOnlyList<NotificationDeliveryResult>>(devices.Select(_resultFactory).ToArray());
        }
    }

    private sealed class InMemoryMessageNotificationDeliveryRepository : IMessageNotificationDeliveryRepository
    {
        private readonly Dictionary<(Guid OutboxJobId, Guid DeviceId), MessageNotificationDelivery> _deliveries = new();

        public Task EnsurePendingAsync(
            Guid outboxJobId,
            IReadOnlyCollection<Guid> deviceIds,
            DateTime createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            foreach (var deviceId in deviceIds.Distinct())
            {
                var key = (outboxJobId, deviceId);
                if (_deliveries.ContainsKey(key))
                    continue;

                _deliveries[key] = new MessageNotificationDelivery(
                    Guid.NewGuid(),
                    outboxJobId,
                    deviceId,
                    MessageNotificationDeliveryStatuses.Pending,
                    0,
                    null,
                    null,
                    null,
                    null,
                    createdAtUtc,
                    createdAtUtc);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MessageNotificationDelivery>> GetByJobAndDeviceIdsAsync(
            Guid outboxJobId,
            IReadOnlyCollection<Guid> deviceIds,
            CancellationToken cancellationToken = default)
        {
            var deviceIdSet = deviceIds.ToHashSet();
            var deliveries = _deliveries.Values
                .Where(delivery => delivery.OutboxJobId == outboxJobId && deviceIdSet.Contains(delivery.DeviceId))
                .ToArray();
            return Task.FromResult<IReadOnlyList<MessageNotificationDelivery>>(deliveries);
        }

        public Task MarkResultsAsync(
            Guid outboxJobId,
            IReadOnlyCollection<NotificationDeliveryResult> results,
            DateTime attemptedAtUtc,
            CancellationToken cancellationToken = default)
        {
            foreach (var result in results)
            {
                UpdateDelivery(
                    outboxJobId,
                    result.DeviceId,
                    ToDeliveryStatus(result.Status),
                    delivery => delivery.Attempts + 1,
                    result.Error,
                    attemptedAtUtc,
                    result.Status == NotificationDeliveryResultStatus.Succeeded ? attemptedAtUtc : null);
            }

            return Task.CompletedTask;
        }

        public Task MarkDevicesFailedAsync(
            Guid outboxJobId,
            IReadOnlyCollection<Guid> deviceIds,
            string error,
            DateTime failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            foreach (var deviceId in deviceIds)
            {
                UpdateDelivery(
                    outboxJobId,
                    deviceId,
                    MessageNotificationDeliveryStatuses.Failed,
                    delivery => delivery.Attempts,
                    error,
                    failedAtUtc,
                    null);
            }

            return Task.CompletedTask;
        }

        private void UpdateDelivery(
            Guid outboxJobId,
            Guid deviceId,
            string status,
            Func<MessageNotificationDelivery, int> attemptsFactory,
            string? error,
            DateTime updatedAtUtc,
            DateTime? succeededAtUtc)
        {
            var key = (outboxJobId, deviceId);
            if (!_deliveries.TryGetValue(key, out var delivery))
                return;

            _deliveries[key] = delivery with
            {
                Status = status,
                Attempts = attemptsFactory(delivery),
                LastError = error,
                FirstAttemptedAtUtc = delivery.FirstAttemptedAtUtc ?? updatedAtUtc,
                LastAttemptedAtUtc = updatedAtUtc,
                SucceededAtUtc = succeededAtUtc ?? delivery.SucceededAtUtc,
                UpdatedAtUtc = updatedAtUtc
            };
        }

        private static string ToDeliveryStatus(NotificationDeliveryResultStatus status)
        {
            return status switch
            {
                NotificationDeliveryResultStatus.Succeeded => MessageNotificationDeliveryStatuses.Succeeded,
                NotificationDeliveryResultStatus.InvalidDevice => MessageNotificationDeliveryStatuses.InvalidDevice,
                NotificationDeliveryResultStatus.TransientFailure => MessageNotificationDeliveryStatuses.TransientFailure,
                NotificationDeliveryResultStatus.PermanentFailure => MessageNotificationDeliveryStatuses.Failed,
                _ => MessageNotificationDeliveryStatuses.Failed
            };
        }
    }
}
