namespace Harmonie.Application.Features.Voice.HandleLiveKitWebhook;

public sealed record HandleLiveKitWebhookRequest(
    string RawBody,
    string? AuthorizationHeader);
