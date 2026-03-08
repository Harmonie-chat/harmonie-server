using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure.Authentication;
using Harmonie.Infrastructure.Configuration;
using Harmonie.Infrastructure.LiveKit;
using Harmonie.Infrastructure.ObjectStorage;
using Harmonie.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<DatabaseSettings>(configuration.GetSection("Database"));
        services.Configure<LiveKitSettings>(configuration.GetSection("LiveKit"));
        services.Configure<ObjectStorageSettings>(configuration.GetSection("ObjectStorage"));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ILiveKitTokenService, LiveKitTokenService>();
        services.AddScoped<ILiveKitWebhookReceiver, LiveKitWebhookReceiver>();
        services.AddScoped<ILiveKitRoomApiClient, LiveKitSdkRoomApiClient>();
        services.AddScoped<ILiveKitRoomService, LiveKitRoomService>();
        services.AddScoped<IObjectStorageService, LocalFileSystemObjectStorageService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        services.AddScoped(_ => new DbSession(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IGuildMemberRepository, GuildMemberRepository>();
        services.AddScoped<IGuildChannelRepository, GuildChannelRepository>();
        services.AddScoped<IChannelMessageRepository, ChannelMessageRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IDirectMessageRepository, DirectMessageRepository>();
        services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
        return services;
    }
}
