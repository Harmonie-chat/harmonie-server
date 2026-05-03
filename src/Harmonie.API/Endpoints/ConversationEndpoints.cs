using Harmonie.Application.Features.Conversations.CreateGroupConversation;
using Harmonie.Application.Features.Conversations.DeleteConversation;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using ConversationAcknowledgeRead = Harmonie.Application.Features.Conversations.AcknowledgeRead.AcknowledgeReadEndpoint;
using ConversationAddReaction = Harmonie.Application.Features.Conversations.AddReaction.AddReactionEndpoint;
using ConversationDeleteMessage = Harmonie.Application.Features.Conversations.DeleteMessage.DeleteMessageEndpoint;
using ConversationDeleteMessageAttachment = Harmonie.Application.Features.Conversations.DeleteMessageAttachment.DeleteMessageAttachmentEndpoint;
using ConversationEditMessage = Harmonie.Application.Features.Conversations.EditMessage.EditMessageEndpoint;
using ConversationGetMessages = Harmonie.Application.Features.Conversations.GetMessages.GetMessagesEndpoint;
using ConversationGetReactionUsers = Harmonie.Application.Features.Conversations.GetReactionUsers.GetReactionUsersEndpoint;
using ConversationPinMessage = Harmonie.Application.Features.Conversations.PinMessage.PinMessageEndpoint;
using ConversationRemoveReaction = Harmonie.Application.Features.Conversations.RemoveReaction.RemoveReactionEndpoint;
using ConversationSendMessage = Harmonie.Application.Features.Conversations.SendMessage.SendMessageEndpoint;
using ConversationUnpinMessage = Harmonie.Application.Features.Conversations.UnpinMessage.UnpinMessageEndpoint;
using ConversationUpdateGroup = Harmonie.Application.Features.Conversations.UpdateGroupConversation.UpdateGroupConversationEndpoint;

namespace Harmonie.API.Endpoints;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        OpenConversationEndpoint.Map(app);
        CreateGroupConversationEndpoint.Map(app);
        ConversationUpdateGroup.Map(app);
        DeleteConversationEndpoint.Map(app);
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
        ConversationGetReactionUsers.Map(app);

        // Pins
        ConversationPinMessage.Map(app);
        ConversationUnpinMessage.Map(app);
    }
}
