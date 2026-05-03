using Harmonie.API.RealTime.Channels;
using Harmonie.API.RealTime.Common;
using Harmonie.API.RealTime.Conversations;
using Harmonie.API.RealTime.Guilds;
using Harmonie.API.RealTime.Messages;
using Harmonie.API.RealTime.Users;
using Harmonie.API.RealTime.Voice;
using Harmonie.API.SignalRDoc.Extensions;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Interfaces.Voice;

namespace Harmonie.API.Configuration;

public static class RealTimeConfiguration
{
    public static IServiceCollection AddRealTime(this IServiceCollection services)
    {
        services.AddSignalR();

        services.AddScoped<ITextChannelNotifier, SignalRTextChannelNotifier>();
        services.AddScoped<IGuildNotifier, SignalRGuildNotifier>();
        services.AddScoped<IVoicePresenceNotifier, SignalRVoicePresenceNotifier>();
        services.AddSingleton<IVoiceParticipantCache, InMemoryVoiceParticipantCache>();
        services.AddScoped<IConversationMessageNotifier, SignalRConversationMessageNotifier>();
        services.AddScoped<IConversationNotifier, SignalRConversationNotifier>();
        services.AddScoped<IUserPresenceNotifier, SignalRUserPresenceNotifier>();
        services.AddScoped<IUserProfileNotifier, SignalRUserProfileNotifier>();
        services.AddScoped<IReactionNotifier, SignalRReactionNotifier>();
        services.AddScoped<IPinNotifier, SignalRPinNotifier>();
        services.AddSingleton<IConnectionTracker, ConnectionTracker>();
        services.AddScoped<IRealtimeGroupManager, SignalRRealtimeGroupManager>();

        services.AddSignalRAsyncApiDoc(options =>
        {
            options.Title = "Harmonie SignalR API";
            options.Version = "1.0.0";
            options.Description = "Real-time events for the Harmonie chat platform.";
            options.HubRoutes[typeof(RealtimeHub)] = "/hubs/realtime";
            options.Assemblies.Add(typeof(RealtimeHub).Assembly);

            options.MethodTags["Guilds"] = ["Guild", "Member", "YouWere", "Channel"];
            options.MethodTags["Conversations"] = ["Conversation", "StartTypingConversation"];
            options.MethodTags["Voice"] = ["Voice"];
            options.MethodTags["Messages"] = ["Message", "Reaction"];
            options.MethodTags["Users"] = ["User", "Presence", "Profile"];
            options.MethodTags["Typing"] = ["StartTypingChannel", "UserTyping"];
            options.MethodTags["Lifecycle"] = ["Ready"];
        });

        return services;
    }
}
