namespace Harmonie.Application.Features.Voice.HandleLiveKitWebhook;

public sealed record HandleLiveKitWebhookResponse(
    bool Processed,
    string EventType);
