using System.Reflection;
using FluentValidation;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Services;
using Harmonie.Application.Services.Notifications;
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
        services.AddScoped<MessageSendOrchestrator>();
        services.AddScoped<ReactionOrchestrator>();
        services.AddScoped<MessageEditDeleteOrchestrator>();
        services.AddScoped<PinOrchestrator>();
        services.AddScoped<ReadOrchestrator>();
        services.AddScoped<MessageFetchOrchestrator>();
        services.AddScoped<PinnedMessageFetchOrchestrator>();
        services.AddScoped<MessageNotificationRecipientResolver>();
        services.AddScoped<MessageNotificationPayloadFactory>();
        services.AddScoped<INotificationDispatchService, NotificationDispatchService>();

        services.AddAuthHandlers();
        services.AddGuildHandlers();
        services.AddChannelHandlers();
        services.AddConversationHandlers();
        services.AddUserHandlers();
        services.AddUploadHandlers();
        services.AddVoiceHandlers();
        services.AddNotificationHandlers();

        return services;
    }
}
