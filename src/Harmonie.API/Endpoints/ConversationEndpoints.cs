using Harmonie.Application.Features.Conversations.CreateGroupConversation;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using ConversationAcknowledgeRead = Harmonie.Application.Features.Conversations.AcknowledgeRead.AcknowledgeReadEndpoint;
using ConversationAddReaction = Harmonie.Application.Features.Conversations.AddReaction.AddReactionEndpoint;
using ConversationDeleteMessage = Harmonie.Application.Features.Conversations.DeleteMessage.DeleteMessageEndpoint;
using ConversationDeleteMessageAttachment = Harmonie.Application.Features.Conversations.DeleteMessageAttachment.DeleteMessageAttachmentEndpoint;
using ConversationEditMessage = Harmonie.Application.Features.Conversations.EditMessage.EditMessageEndpoint;
using ConversationGetMessages = Harmonie.Application.Features.Conversations.GetMessages.GetMessagesEndpoint;
using ConversationRemoveReaction = Harmonie.Application.Features.Conversations.RemoveReaction.RemoveReactionEndpoint;
using ConversationSendMessage = Harmonie.Application.Features.Conversations.SendMessage.SendMessageEndpoint;

namespace Harmonie.API.Endpoints;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        OpenConversationEndpoint.Map(app);
        CreateGroupConversationEndpoint.Map(app);
        ListConversationsEndpoint.Map(app);
        SearchConversationMessagesEndpoint.Map(app);

        // Messages
        ConversationSendMessage.Map(app);
        ConversationGetMessages.Map(app);
        ConversationEditMessage.Map(app);
        ConversationDeleteMessage.Map(app);
        ConversationDeleteMessageAttachment.Map(app);
        ConversationAcknowledgeRead.Map(app);

        // Reactions
        ConversationAddReaction.Map(app);
        ConversationRemoveReaction.Map(app);
    }
}
