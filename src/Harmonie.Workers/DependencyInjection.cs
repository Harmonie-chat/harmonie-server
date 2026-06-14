using Harmonie.Application;
using Harmonie.Application.Services.Notifications;
using Harmonie.Infrastructure;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Workers;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddNotificationApplicationServices();
        services.AddPersistence(configuration);
        services.AddNotificationDeliveryInfrastructure(configuration);
        services.AddOptions<PushNotificationOptions>()
            .Bind(configuration.GetSection(PushNotificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IPushNotificationBatchProcessor, PushNotificationBatchProcessor>();
        services.AddHostedService<PushNotificationWorker>();

        return services;
    }
}
