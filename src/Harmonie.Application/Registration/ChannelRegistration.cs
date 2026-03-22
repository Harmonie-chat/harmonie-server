using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.AcknowledgeRead;
using Harmonie.Application.Features.Channels.AddReaction;
using Harmonie.Application.Features.Channels.DeleteChannel;
using Harmonie.Application.Features.Channels.DeleteMessage;
using Harmonie.Application.Features.Channels.DeleteMessageAttachment;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Channels.RemoveReaction;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Channels.UpdateChannel;
using Harmonie.Domain.ValueObjects.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class ChannelRegistration
{
    public static IServiceCollection AddChannelHandlers(this IServiceCollection services)
    {
        services.AddAuthenticatedHandler<UpdateChannelInput, UpdateChannelResponse, UpdateChannelHandler>();
        services.AddAuthenticatedHandler<GuildChannelId, bool, DeleteChannelHandler>();
        services.AddAuthenticatedHandler<GuildChannelId, JoinVoiceChannelResponse, JoinVoiceChannelHandler>();

        // Messages
        services.AddAuthenticatedHandler<SendChannelMessageInput, SendMessageResponse, SendMessageHandler>();
        services.AddAuthenticatedHandler<GetChannelMessagesInput, GetMessagesResponse, GetMessagesHandler>();
        services.AddAuthenticatedHandler<EditChannelMessageInput, EditMessageResponse, EditMessageHandler>();
        services.AddAuthenticatedHandler<DeleteChannelMessageInput, bool, DeleteMessageHandler>();
        services.AddAuthenticatedHandler<DeleteChannelMessageAttachmentInput, bool, DeleteMessageAttachmentHandler>();
        services.AddAuthenticatedHandler<AcknowledgeChannelReadInput, bool, AcknowledgeReadHandler>();

        // Reactions
        services.AddAuthenticatedHandler<ChannelAddReactionInput, bool, AddReactionHandler>();
        services.AddAuthenticatedHandler<ChannelRemoveReactionInput, bool, RemoveReactionHandler>();

        return services;
    }
}
