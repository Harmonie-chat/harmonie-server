using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure.Authentication;
using Harmonie.Infrastructure.Configuration;
using Harmonie.Infrastructure.HealthChecks;
using Harmonie.Infrastructure.LiveKit;
using Harmonie.Infrastructure.ObjectStorage;
using Harmonie.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Harmonie.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<DatabaseSettings>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<LiveKitSettings>()
            .Bind(configuration.GetSection("LiveKit"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ObjectStorageSettings>()
            .Bind(configuration.GetSection("ObjectStorage"))
            .ValidateDataAnnotations()
            .Validate(
                settings => Uri.TryCreate(settings.LocalBaseUrl, UriKind.Absolute, out _),
                "ObjectStorage:LocalBaseUrl must be a valid absolute URL.")
            .ValidateOnStart();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ILiveKitTokenService, LiveKitTokenService>();
        services.AddScoped<ILiveKitWebhookReceiver, LiveKitWebhookReceiver>();
        services.AddSingleton(sp =>
        {
            _ = sp.GetRequiredService<IOptions<LiveKitSettings>>().Value;
            return new HttpClient();
        });
        services.AddScoped<ILiveKitRoomApiClient, LiveKitSdkRoomApiClient>();
        services.AddScoped<ILiveKitRoomService, LiveKitRoomService>();
        services.AddScoped<IObjectStorageService, LocalFileSystemObjectStorageService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        services.AddScoped(_ => new DbSession(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IGuildMemberRepository, GuildMemberRepository>();
        services.AddScoped<IGuildInviteRepository, GuildInviteRepository>();
        services.AddScoped<IGuildChannelRepository, GuildChannelRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
        return services;
    }
}
