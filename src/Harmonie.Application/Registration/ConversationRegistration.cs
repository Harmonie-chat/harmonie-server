using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.AcknowledgeRead;
using Harmonie.Application.Features.Conversations.AddReaction;
using Harmonie.Application.Features.Conversations.CreateGroupConversation;
using Harmonie.Application.Features.Conversations.DeleteConversation;
using Harmonie.Application.Features.Conversations.DeleteMessage;
using Harmonie.Application.Features.Conversations.DeleteMessageAttachment;
using Harmonie.Application.Features.Conversations.EditMessage;
using Harmonie.Application.Features.Conversations.GetMessages;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.RemoveReaction;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using Harmonie.Application.Features.Conversations.SendMessage;
using Harmonie.Application.Features.Conversations.UpdateGroupConversation;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class ConversationRegistration
{
    public static IServiceCollection AddConversationHandlers(this IServiceCollection services)
    {
        services.AddAuthenticatedHandler<OpenConversationRequest, OpenConversationResponse, OpenConversationHandler>();
        services.AddAuthenticatedHandler<CreateGroupConversationRequest, CreateGroupConversationResponse, CreateGroupConversationHandler>();
        services.AddAuthenticatedHandler<UpdateGroupConversationInput, UpdateGroupConversationResponse, UpdateGroupConversationHandler>();
        services.AddAuthenticatedHandler<DeleteConversationInput, bool, DeleteConversationHandler>();
        services.AddAuthenticatedHandler<Unit, ListConversationsResponse, ListConversationsHandler>();
        services.AddAuthenticatedHandler<SearchConversationMessagesInput, SearchConversationMessagesResponse, SearchConversationMessagesHandler>();

        // Messages
        services.AddAuthenticatedHandler<SendConversationMessageInput, SendMessageResponse, SendMessageHandler>();
        services.AddAuthenticatedHandler<GetConversationMessagesInput, GetMessagesResponse, GetMessagesHandler>();
        services.AddAuthenticatedHandler<EditConversationMessageInput, EditMessageResponse, EditMessageHandler>();
        services.AddAuthenticatedHandler<DeleteConversationMessageInput, bool, DeleteMessageHandler>();
        services.AddAuthenticatedHandler<DeleteConversationMessageAttachmentInput, bool, DeleteMessageAttachmentHandler>();
        services.AddAuthenticatedHandler<AcknowledgeConversationReadInput, bool, AcknowledgeReadHandler>();

        // Reactions
        services.AddAuthenticatedHandler<ConversationAddReactionInput, bool, AddReactionHandler>();
        services.AddAuthenticatedHandler<ConversationRemoveReactionInput, bool, RemoveReactionHandler>();

        return services;
    }
}
