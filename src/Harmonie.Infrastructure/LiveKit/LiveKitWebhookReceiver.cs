using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure.Configuration;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonie.Infrastructure.LiveKit;

public sealed class LiveKitWebhookReceiver : ILiveKitWebhookReceiver
{
    private readonly WebhookReceiver _webhookReceiver;
    private readonly ILogger<LiveKitWebhookReceiver> _logger;

    public LiveKitWebhookReceiver(
        IOptions<LiveKitSettings> settings,
        ILogger<LiveKitWebhookReceiver> logger)
    {
        _logger = logger;

        var liveKitSettings = settings.Value;
        if (string.IsNullOrWhiteSpace(liveKitSettings.ApiKey))
            throw new InvalidOperationException("Configuration value 'LiveKit:ApiKey' is required.");

        if (string.IsNullOrWhiteSpace(liveKitSettings.ApiSecret))
            throw new InvalidOperationException("Configuration value 'LiveKit:ApiSecret' is required.");

        _webhookReceiver = new WebhookReceiver(liveKitSettings.ApiKey, liveKitSettings.ApiSecret);
    }

    public LiveKitWebhookReceiveResult Receive(
        string rawBody,
        string authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return LiveKitWebhookReceiveResult.Fail("Webhook body is required.");

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return LiveKitWebhookReceiveResult.Fail("Authorization header is required.");

        try
        {
            var normalizedAuthorizationHeader = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorizationHeader["Bearer ".Length..].Trim()
                : authorizationHeader.Trim();

            var webhookEvent = _webhookReceiver.Receive(
                rawBody,
                normalizedAuthorizationHeader,
                skipAuth: false,
                ignoreUnknownFields: false);

            var roomName = string.IsNullOrWhiteSpace(webhookEvent.Room?.Name)
                ? null
                : webhookEvent.Room.Name;
            var participantIdentity = string.IsNullOrWhiteSpace(webhookEvent.Participant?.Identity)
                ? null
                : webhookEvent.Participant.Identity;
            var participantName = string.IsNullOrWhiteSpace(webhookEvent.Participant?.Name)
                ? null
                : webhookEvent.Participant.Name;
            var occurredAtUtc = webhookEvent.CreatedAt > 0
                ? DateTimeOffset.FromUnixTimeSeconds(webhookEvent.CreatedAt).UtcDateTime
                : DateTime.UnixEpoch;

            return LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    webhookEvent.Event,
                    roomName,
                    participantIdentity,
                    participantName,
                    occurredAtUtc));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LiveKit webhook validation failed.");
            return LiveKitWebhookReceiveResult.Fail("LiveKit webhook validation failed.");
        }
    }
}
