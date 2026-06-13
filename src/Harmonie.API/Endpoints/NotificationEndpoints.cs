using Harmonie.Application.Features.Notifications.RegisterWebPushDevice;

namespace Harmonie.API.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterWebPushDeviceEndpoint.Map(app);
    }
}
