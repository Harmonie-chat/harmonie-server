using System.Reflection;
using FluentValidation;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Messages.ResolveLinkPreviews;
using Harmonie.Application.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<UploadedFileCleanupService>();
        services.AddScoped<MessageAttachmentResolver>();
        services.AddScoped<LinkPreviewResolutionService>();

        services.AddAuthHandlers();
        services.AddGuildHandlers();
        services.AddChannelHandlers();
        services.AddConversationHandlers();
        services.AddUserHandlers();
        services.AddUploadHandlers();
        services.AddVoiceHandlers();

        return services;
    }
}
