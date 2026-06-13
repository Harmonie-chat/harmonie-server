using Harmonie.Infrastructure;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHostedService<PushNotificationWorker>();

await builder.Build().RunAsync();
