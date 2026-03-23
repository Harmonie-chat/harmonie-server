using Harmonie.Application.Common;
using Harmonie.Application.Features.Voice.HandleLiveKitWebhook;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class VoiceRegistration
{
    public static IServiceCollection AddVoiceHandlers(this IServiceCollection services)
    {
        services.AddHandler<HandleLiveKitWebhookRequest, HandleLiveKitWebhookResponse, HandleLiveKitWebhookHandler>();

        return services;
    }
}
