using Harmonie.Application.Common;
using Harmonie.Application.Features.Notifications.RegisterWebPushDevice;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class NotificationRegistration
{
    public static IServiceCollection AddNotificationHandlers(this IServiceCollection services)
    {
        services.AddAuthenticatedHandler<RegisterWebPushDeviceRequest, bool, RegisterWebPushDeviceHandler>();

        return services;
    }
}
