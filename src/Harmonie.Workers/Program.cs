using Harmonie.Application;
using Harmonie.Application.Services.Notifications;
using Harmonie.Infrastructure;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddNotificationDeliveryInfrastructure(builder.Configuration);
builder.Services.AddOptions<PushNotificationOptions>()
    .Bind(builder.Configuration.GetSection(PushNotificationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<IPushNotificationBatchProcessor, PushNotificationBatchProcessor>();
builder.Services.AddHostedService<PushNotificationWorker>();

await builder.Build().RunAsync();
