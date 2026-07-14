using Harmonie.Application.Features.Notifications.GetWebPushPublicKey;
using Harmonie.Application.Features.Notifications.RegisterWebPushDevice;

namespace Harmonie.API.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        GetWebPushPublicKeyEndpoint.Map(app);
        RegisterWebPushDeviceEndpoint.Map(app);
    }
}
