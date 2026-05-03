using Harmonie.Application.Features.Channels.AcknowledgeRead;
using Harmonie.Application.Features.Channels.DeleteChannel;
using Harmonie.Application.Features.Channels.DeleteMessageAttachment;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Channels.UpdateChannel;
using ChannelAddReaction = Harmonie.Application.Features.Channels.AddReaction.AddReactionEndpoint;
using ChannelDeleteMessage = Harmonie.Application.Features.Channels.DeleteMessage.DeleteMessageEndpoint;
using ChannelEditMessage = Harmonie.Application.Features.Channels.EditMessage.EditMessageEndpoint;
using ChannelGetMessages = Harmonie.Application.Features.Channels.GetMessages.GetMessagesEndpoint;
using ChannelGetReactionUsers = Harmonie.Application.Features.Channels.GetReactionUsers.GetReactionUsersEndpoint;
using ChannelPinMessage = Harmonie.Application.Features.Channels.PinMessage.PinMessageEndpoint;
using ChannelRemoveReaction = Harmonie.Application.Features.Channels.RemoveReaction.RemoveReactionEndpoint;
using ChannelSendMessage = Harmonie.Application.Features.Channels.SendMessage.SendMessageEndpoint;
using ChannelUnpinMessage = Harmonie.Application.Features.Channels.UnpinMessage.UnpinMessageEndpoint;

namespace Harmonie.API.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this IEndpointRouteBuilder app)
    {
        UpdateChannelEndpoint.Map(app);
        DeleteChannelEndpoint.Map(app);
        JoinVoiceChannelEndpoint.Map(app);

        // Messages
        ChannelSendMessage.Map(app);
        ChannelGetMessages.Map(app);
        ChannelEditMessage.Map(app);
        ChannelDeleteMessage.Map(app);
        DeleteMessageAttachmentEndpoint.Map(app);
        AcknowledgeReadEndpoint.Map(app);

        // Reactions
        ChannelAddReaction.Map(app);
        ChannelRemoveReaction.Map(app);
        ChannelGetReactionUsers.Map(app);

        // Pins
        ChannelPinMessage.Map(app);
        ChannelUnpinMessage.Map(app);
    }
}
