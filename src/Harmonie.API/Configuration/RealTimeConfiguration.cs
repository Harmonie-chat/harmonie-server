using Harmonie.API.RealTime.Channels;
using Harmonie.API.RealTime.Common;
using Harmonie.API.RealTime.Conversations;
using Harmonie.API.RealTime.Guilds;
using Harmonie.API.RealTime.Messages;
using Harmonie.API.RealTime.Users;
using Harmonie.API.RealTime.Voice;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Interfaces.Voice;
using Saunter;
using Saunter.AsyncApiSchema.v2;

namespace Harmonie.API.Configuration;

public static class RealTimeConfiguration
{
    public static IServiceCollection AddRealTime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddAsyncApiSchemaGeneration(options =>
        {
            options.AssemblyMarkerTypes = new[] { typeof(RealtimeHubDocumentation) };
            options.Middleware.UiTitle = "Harmonie Realtime API";
            options.AsyncApi = new AsyncApiDocument
            {
                Info = new Info("Harmonie Realtime API", "1.0.0")
                {
                    Description = "Real-time events for the Harmonie communication platform, served over SignalR (WebSocket).",
                },
                Servers =
                {
                    ["signalr"] = new Server("/hubs/realtime", "ws")
                    {
                        Description = "SignalR hub — requires Bearer JWT via the access_token query parameter.",
                    },
                },
            };
        });

        services.AddScoped<ITextChannelNotifier, SignalRTextChannelNotifier>();
        services.AddScoped<IGuildNotifier, SignalRGuildNotifier>();
        services.AddScoped<IVoicePresenceNotifier, SignalRVoicePresenceNotifier>();
        services.AddScoped<IConversationMessageNotifier, SignalRConversationMessageNotifier>();
        services.AddScoped<IUserPresenceNotifier, SignalRUserPresenceNotifier>();
        services.AddScoped<IReactionNotifier, SignalRReactionNotifier>();
        services.AddSingleton<IConnectionTracker, ConnectionTracker>();
        services.AddScoped<IRealtimeGroupManager, SignalRRealtimeGroupManager>();

        return services;
    }
}
