namespace Harmonie.Application.Interfaces;

public interface ILiveKitWebhookReceiver
{
    LiveKitWebhookReceiveResult Receive(
        string rawBody,
        string authorizationHeader);
}

public sealed record LiveKitWebhookReceiveResult(
    bool Success,
    LiveKitWebhookEvent? Event,
    string? ErrorDetail)
{
    public static LiveKitWebhookReceiveResult Ok(LiveKitWebhookEvent webhookEvent)
        => new(true, webhookEvent, null);

    public static LiveKitWebhookReceiveResult Fail(string detail)
        => new(false, null, detail);
}

public sealed record LiveKitWebhookEvent(
    string EventType,
    string? RoomName,
    string? ParticipantIdentity,
    string? ParticipantName,
    DateTime OccurredAtUtc);
