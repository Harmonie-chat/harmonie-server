using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Application.Services.Notifications;
using Harmonie.Workers;
using Harmonie.Workers.Workers.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Harmonie.Workers.Tests;

public sealed class WorkerDependencyInjectionTests
{
    [Fact]
    public async Task WorkerServices_ShouldBuildWithoutApiOnlyApplicationHandlers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=postgres;Port=5432;Database=harmonie;Username=harmonie_user;Password=harmonie_password",
                ["PushNotifications:Enabled"] = "false",
                ["PushNotifications:BatchSize"] = "100",
                ["PushNotifications:PollIntervalSeconds"] = "30",
                ["PushNotifications:LockDurationSeconds"] = "300",
                ["PushNotifications:MaxConcurrentJobs"] = "4",
                ["PushNotifications:MaxAttempts"] = "5",
                ["PushNotifications:RetryBaseDelaySeconds"] = "30",
                ["NotificationCleanup:Enabled"] = "false",
                ["NotificationCleanup:PollIntervalSeconds"] = "86400",
                ["NotificationCleanup:BatchSize"] = "500",
                ["NotificationCleanup:ProcessedOutboxRetentionDays"] = "7",
                ["NotificationCleanup:FailedOutboxRetentionDays"] = "30",
                ["NotificationCleanup:ExpiredDeviceRetentionDays"] = "7",
                ["VAPID_PUBLIC_KEY"] = string.Empty,
                ["VAPID_PRIVATE_KEY"] = string.Empty,
                ["VAPID_SUBJECT"] = "mailto:contact@harmonie.app"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddWorkerServices(configuration);

        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<INotificationDispatchService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPushNotificationBatchProcessor>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<INotificationCleanupProcessor>().Should().NotBeNull();
        serviceProvider.GetServices<IHostedService>().Should().ContainSingle(service => service is PushNotificationWorker);
        serviceProvider.GetServices<IHostedService>().Should().ContainSingle(service => service is NotificationCleanupWorker);
        scope.ServiceProvider.GetService<IUserPresenceNotifier>().Should().BeNull();
        scope.ServiceProvider.GetService<IObjectStorageService>().Should().BeNull();
        scope.ServiceProvider.GetService<ILiveKitWebhookReceiver>().Should().BeNull();
    }
}
