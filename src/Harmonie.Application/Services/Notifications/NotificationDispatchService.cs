using Harmonie.Application.Interfaces.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Application.Services.Notifications;

public sealed class NotificationDispatchService : INotificationDispatchService
{
    private readonly IMessageNotificationContextRepository _contextRepository;
    private readonly INotificationDeviceRepository _deviceRepository;
    private readonly IMessageNotificationOutboxRepository _outboxRepository;
    private readonly IMessageNotificationDeliveryRepository _deliveryRepository;
    private readonly IMessageNotificationPolicy _notificationPolicy;
    private readonly MessageNotificationPayloadFactory _payloadFactory;
    private readonly IReadOnlyDictionary<string, INotificationDeliveryAdapter> _adaptersByPlatform;
    private readonly PushNotificationOptions _options;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        IMessageNotificationContextRepository contextRepository,
        INotificationDeviceRepository deviceRepository,
        IMessageNotificationOutboxRepository outboxRepository,
        IMessageNotificationDeliveryRepository deliveryRepository,
        IMessageNotificationPolicy notificationPolicy,
        MessageNotificationPayloadFactory payloadFactory,
        IEnumerable<INotificationDeliveryAdapter> adapters,
        IOptions<PushNotificationOptions> options,
        ILogger<NotificationDispatchService> logger)
    {
        _contextRepository = contextRepository;
        _deviceRepository = deviceRepository;
        _outboxRepository = outboxRepository;
        _deliveryRepository = deliveryRepository;
        _notificationPolicy = notificationPolicy;
        _payloadFactory = payloadFactory;
        _adaptersByPlatform = adapters
            .GroupBy(adapter => adapter.Platform, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _logger = logger;
    }

    public async Task DispatchAsync(
        MessageNotificationOutboxJob job,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextRepository.GetAsync(job.MessageId, cancellationToken);
        if (context is null)
        {
            _logger.LogInformation(
                "Message notification outbox job {JobId} skipped because message {MessageId} no longer exists or is deleted.",
                job.Id,
                job.MessageId.Value);
            await _outboxRepository.MarkProcessedAsync(job.Id, nowUtc, cancellationToken);
            return;
        }

        var recipientIds = _notificationPolicy.SelectRecipients(context);
        if (recipientIds.Count == 0)
        {
            _logger.LogDebug(
                "Message notification outbox job {JobId} completed without recipients for message {MessageId}.",
                job.Id,
                job.MessageId.Value);
            await _outboxRepository.MarkProcessedAsync(job.Id, nowUtc, cancellationToken);
            return;
        }

        var devices = await _deviceRepository.GetActiveByUserIdsAsync(recipientIds, nowUtc, cancellationToken);
        if (devices.Count == 0)
        {
            _logger.LogDebug(
                "Message notification outbox job {JobId} completed because {RecipientCount} recipients have no active notification devices.",
                job.Id,
                recipientIds.Count);
            await _outboxRepository.MarkProcessedAsync(job.Id, nowUtc, cancellationToken);
            return;
        }

        var deviceIds = devices.Select(device => device.Id).Distinct().ToArray();
        var retryableDeviceIds = await _deliveryRepository.GetOrCreateRetryableDeviceIdsAsync(
            job.Id,
            deviceIds,
            nowUtc,
            cancellationToken);
        var devicesToAttempt = devices
            .Where(device => retryableDeviceIds.Contains(device.Id))
            .ToArray();

        if (devicesToAttempt.Length == 0)
        {
            _logger.LogDebug(
                "Message notification outbox job {JobId} processed because all active notification devices are already in terminal delivery states.",
                job.Id);
            await _outboxRepository.MarkProcessedAsync(job.Id, nowUtc, cancellationToken);
            return;
        }

        var payload = _payloadFactory.Create(context);
        var deliveryResults = new List<NotificationDeliveryResult>();
        var unsupportedFailures = new List<(Guid DeviceId, string Error)>();

        foreach (var platformDevices in devicesToAttempt.GroupBy(device => device.Platform, StringComparer.OrdinalIgnoreCase))
        {
            var platformDeviceArray = platformDevices.ToArray();
            if (!_adaptersByPlatform.TryGetValue(platformDevices.Key, out var adapter))
            {
                var unsupportedError = $"No notification adapter registered for platform '{platformDevices.Key}'";
                _logger.LogWarning(
                    "Message notification outbox job {JobId} found {DeviceCount} devices for unsupported notification platform {Platform}.",
                    job.Id,
                    platformDeviceArray.Length,
                    platformDevices.Key);
                unsupportedFailures.AddRange(platformDeviceArray.Select(device => (device.Id, unsupportedError)));
                continue;
            }

            var results = await adapter.SendAsync(payload, platformDeviceArray, cancellationToken);
            deliveryResults.AddRange(results);
        }

        if (deliveryResults.Count > 0)
        {
            await _deliveryRepository.MarkResultsAsync(job.Id, deliveryResults, nowUtc, cancellationToken);
        }

        if (unsupportedFailures.Count > 0)
        {
            foreach (var failureGroup in unsupportedFailures.GroupBy(failure => failure.Error, StringComparer.Ordinal))
            {
                await _deliveryRepository.MarkDevicesFailedAsync(
                    job.Id,
                    failureGroup.Select(failure => failure.DeviceId).ToArray(),
                    failureGroup.Key,
                    nowUtc,
                    cancellationToken);
            }
        }

        var invalidDeviceIds = deliveryResults
            .Where(result => result.Status == NotificationDeliveryResultStatus.InvalidDevice)
            .Select(result => result.DeviceId)
            .Distinct()
            .ToArray();
        if (invalidDeviceIds.Length > 0)
        {
            _logger.LogInformation(
                "Removing {InvalidDeviceCount} invalid notification devices after message notification outbox job {JobId}.",
                invalidDeviceIds.Length,
                job.Id);
            await _deviceRepository.DeleteManyAsync(invalidDeviceIds, cancellationToken);
        }

        var transientFailures = deliveryResults
            .Where(result => result.Status == NotificationDeliveryResultStatus.TransientFailure)
            .ToArray();

        if (transientFailures.Length == 0)
        {
            _logger.LogDebug(
                "Message notification outbox job {JobId} processed for {AttemptedDeviceCount} attempted devices.",
                job.Id,
                devicesToAttempt.Length);
            await _outboxRepository.MarkProcessedAsync(job.Id, nowUtc, cancellationToken);
            return;
        }

        var error = string.Join(
            "; ",
            transientFailures
                .Select(result => result.Error ?? result.Status.ToString())
                .Distinct(StringComparer.Ordinal)
                .Take(5));
        if (job.Attempts >= _options.MaxAttempts)
        {
            _logger.LogWarning(
                "Message notification outbox job {JobId} failed after {Attempts} attempts: {Error}",
                job.Id,
                job.Attempts,
                error);
            await _deliveryRepository.MarkDevicesFailedAsync(
                job.Id,
                transientFailures.Select(result => result.DeviceId).Distinct().ToArray(),
                error,
                nowUtc,
                cancellationToken);
            await _outboxRepository.MarkFailedAsync(job.Id, error, nowUtc, cancellationToken);
            return;
        }

        var nextAttemptAtUtc = nowUtc.Add(CalculateRetryDelay(job.Attempts));
        _logger.LogWarning(
            "Message notification outbox job {JobId} attempt {Attempts} failed and will retry at {NextAttemptAtUtc}: {Error}",
            job.Id,
            job.Attempts,
            nextAttemptAtUtc,
            error);
        await _outboxRepository.ScheduleRetryAsync(job.Id, nextAttemptAtUtc, error, cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int attempts)
    {
        var exponent = Math.Max(0, attempts - 1);
        var multiplier = Math.Pow(2, Math.Min(exponent, 6));
        return TimeSpan.FromSeconds(_options.RetryBaseDelaySeconds * multiplier);
    }
}
