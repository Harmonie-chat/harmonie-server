using Harmonie.Application.Interfaces.Auth;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Infrastructure.Authentication;
using Harmonie.Infrastructure.Configuration;
using Harmonie.Infrastructure.LiveKit;
using Harmonie.Infrastructure.ObjectStorage;
using Harmonie.Infrastructure.Persistence.Auth;
using Harmonie.Infrastructure.Persistence.Channels;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Persistence.Conversations;
using Harmonie.Infrastructure.Persistence.Guilds;
using Harmonie.Infrastructure.Persistence.Messages;
using Harmonie.Infrastructure.Persistence.Notifications;
using Harmonie.Infrastructure.Persistence.Uploads;
using Harmonie.Infrastructure.Persistence.Users;
using Harmonie.Infrastructure.Services;
using Harmonie.Infrastructure.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebPush;

namespace Harmonie.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddAuthInfrastructure(configuration);
        services.AddLiveKitInfrastructure(configuration);
        services.AddObjectStorageInfrastructure(configuration);
        services.AddLinkPreviewInfrastructure();
        services.AddWebPushConfiguration(configuration);

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseSettings>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        services.AddScoped(_ => new DbSession(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IGuildMemberRepository, GuildMemberRepository>();
        services.AddScoped<IGuildBanRepository, GuildBanRepository>();
        services.AddScoped<IGuildInviteRepository, GuildInviteRepository>();
        services.AddScoped<IGuildChannelRepository, GuildChannelRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessagePaginationRepository, MessagePaginationRepository>();
        services.AddScoped<IMessageSearchRepository, MessageSearchRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationParticipantRepository, ConversationParticipantRepository>();
        services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
        services.AddScoped<IMessageReactionRepository, MessageReactionRepository>();
        services.AddScoped<IMessageAttachmentRepository, MessageAttachmentRepository>();
        services.AddScoped<IPinnedMessageRepository, PinnedMessageRepository>();
        services.AddScoped<ILinkPreviewRepository, LinkPreviewRepository>();
        services.AddScoped<IChannelReadStateRepository, ChannelReadStateRepository>();
        services.AddScoped<IConversationReadStateRepository, ConversationReadStateRepository>();
        services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
        services.AddScoped<INotificationDeviceRepository, NotificationDeviceRepository>();
        services.AddScoped<INotificationCleanupRepository, NotificationCleanupRepository>();
        services.AddScoped<IMessageNotificationOutboxRepository, MessageNotificationOutboxRepository>();
        services.AddScoped<IMessageNotificationDeliveryRepository, MessageNotificationDeliveryRepository>();
        services.AddScoped<IMessageNotificationContextRepository, MessageNotificationContextRepository>();

        return services;
    }

    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    public static IServiceCollection AddLiveKitInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LiveKitSettings>()
            .Bind(configuration.GetSection("LiveKit"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            _ = sp.GetRequiredService<IOptions<LiveKitSettings>>().Value;
            return new HttpClient();
        });
        services.AddSingleton<ILiveKitRoomApiClient, LiveKitSdkRoomApiClient>();
        services.AddScoped<ILiveKitTokenService, LiveKitTokenService>();
        services.AddScoped<ILiveKitWebhookReceiver, LiveKitWebhookReceiver>();
        services.AddScoped<ILiveKitRoomService, LiveKitRoomService>();

        return services;
    }

    public static IServiceCollection AddObjectStorageInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ObjectStorageSettings>()
            .Bind(configuration.GetSection("ObjectStorage"))
            .ValidateDataAnnotations()
            .Validate(
                settings => Uri.TryCreate(settings.LocalBaseUrl, UriKind.Absolute, out _),
                "ObjectStorage:LocalBaseUrl must be a valid absolute URL.")
            .ValidateOnStart();

        services.AddScoped<IObjectStorageService, LocalFileSystemObjectStorageService>();

        return services;
    }

    public static IServiceCollection AddLinkPreviewInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<ILinkPreviewFetcher, LinkPreviewFetcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Harmonie-LinkPreview/1.0");
        });

        return services;
    }

    public static IServiceCollection AddWebPushConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebPushSettings>()
            .Configure(options =>
            {
                configuration.GetSection(WebPushSettings.SectionName).Bind(options);
                options.PublicKey = configuration["VAPID_PUBLIC_KEY"] ?? options.PublicKey;
                options.PrivateKey = configuration["VAPID_PRIVATE_KEY"] ?? options.PrivateKey;
                options.Subject = configuration["VAPID_SUBJECT"] ?? options.Subject;
            })
            .Validate(
                settings => !settings.HasVapidCredentials
                            || Uri.TryCreate(settings.Subject, UriKind.Absolute, out _)
                            || settings.Subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase),
                "WebPush:Subject must be an absolute URI or mailto URI when VAPID credentials are configured.")
            .ValidateOnStart();

        services.AddSingleton<IWebPushPublicKeyProvider, WebPushPublicKeyProvider>();

        return services;
    }

    public static IServiceCollection AddNotificationDeliveryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebPushConfiguration(configuration);

        services.AddSingleton<WebPushClient>();
        services.AddSingleton<WebPushEndpointValidator>();
        services.AddScoped<INotificationDeliveryAdapter, WebPushNotificationDeliveryAdapter>();

        return services;
    }
}
