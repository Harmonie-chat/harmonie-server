namespace Harmonie.Application.Features.Notifications.RegisterWebPushDevice;

public sealed record RegisterWebPushDeviceRequest(
    string Endpoint,
    long? ExpirationTime,
    RegisterWebPushDeviceKeysRequest Keys);

public sealed record RegisterWebPushDeviceKeysRequest(
    string P256dh,
    string Auth);
