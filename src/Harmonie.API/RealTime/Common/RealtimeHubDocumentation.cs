using Harmonie.API.RealTime.Channels;
using Harmonie.API.RealTime.Conversations;
using Harmonie.API.RealTime.Guilds;
using Harmonie.API.RealTime.Messages;
using Harmonie.API.RealTime.Users;
using Harmonie.API.RealTime.Voice;
using Saunter.Attributes;

namespace Harmonie.API.RealTime.Common;

/// <summary>
/// AsyncAPI documentation for the RealtimeHub SignalR hub.
/// This class is not invoked at runtime — it exists solely to generate
/// the AsyncAPI specification via Saunter's attribute scanning.
/// </summary>
[AsyncApi]
public class RealtimeHubDocumentation
{
    // ── Client → Server (Publish) ──────────────────────────────────

    [Channel("hubs/realtime/StartTypingChannel")]
    [PublishOperation(typeof(StartTypingChannelMessage),
        Summary = "Signal that the user is typing in a guild text channel. Throttled to 1 event per 5 seconds per user per channel.")]
    public void StartTypingChannel() { }

    [Channel("hubs/realtime/StartTypingConversation")]
    [PublishOperation(typeof(StartTypingConversationMessage),
        Summary = "Signal that the user is typing in a direct conversation. Throttled to 1 event per 5 seconds per user per conversation.")]
    public void StartTypingConversation() { }

    // ── Server → Client (Subscribe) ───────────────────────────────

    [Channel("hubs/realtime/MessageCreated")]
    [SubscribeOperation(typeof(MessageCreatedEvent),
        Summary = "Received when a new message is posted in a text channel.")]
    public void OnMessageCreated() { }

    [Channel("hubs/realtime/MessageUpdated")]
    [SubscribeOperation(typeof(MessageUpdatedEvent),
        Summary = "Received when a message is edited in a text channel.")]
    public void OnMessageUpdated() { }

    [Channel("hubs/realtime/MessageDeleted")]
    [SubscribeOperation(typeof(MessageDeletedEvent),
        Summary = "Received when a message is deleted in a text channel.")]
    public void OnMessageDeleted() { }

    [Channel("hubs/realtime/ConversationMessageCreated")]
    [SubscribeOperation(typeof(ConversationMessageCreatedEvent),
        Summary = "Received when a new message is posted in a direct conversation.")]
    public void OnConversationMessageCreated() { }

    [Channel("hubs/realtime/ConversationMessageUpdated")]
    [SubscribeOperation(typeof(ConversationMessageUpdatedEvent),
        Summary = "Received when a message is edited in a direct conversation.")]
    public void OnConversationMessageUpdated() { }

    [Channel("hubs/realtime/ConversationMessageDeleted")]
    [SubscribeOperation(typeof(ConversationMessageDeletedEvent),
        Summary = "Received when a message is deleted in a direct conversation.")]
    public void OnConversationMessageDeleted() { }

    [Channel("hubs/realtime/VoiceParticipantJoined")]
    [SubscribeOperation(typeof(VoiceParticipantJoinedEvent),
        Summary = "Received when a user joins a voice channel.")]
    public void OnVoiceParticipantJoined() { }

    [Channel("hubs/realtime/VoiceParticipantLeft")]
    [SubscribeOperation(typeof(VoiceParticipantLeftEvent),
        Summary = "Received when a user leaves a voice channel.")]
    public void OnVoiceParticipantLeft() { }

    [Channel("hubs/realtime/ChannelCreated")]
    [SubscribeOperation(typeof(ChannelCreatedEvent),
        Summary = "Received when a new channel is created in a guild.")]
    public void OnChannelCreated() { }

    [Channel("hubs/realtime/ChannelUpdated")]
    [SubscribeOperation(typeof(ChannelUpdatedEvent),
        Summary = "Received when a channel's name or position is updated in a guild.")]
    public void OnChannelUpdated() { }

    [Channel("hubs/realtime/ChannelDeleted")]
    [SubscribeOperation(typeof(ChannelDeletedEvent),
        Summary = "Received when a channel is deleted from a guild.")]
    public void OnChannelDeleted() { }

    [Channel("hubs/realtime/ChannelsReordered")]
    [SubscribeOperation(typeof(ChannelsReorderedEvent),
        Summary = "Received when a guild admin reorders channels.")]
    public void OnChannelsReordered() { }

    [Channel("hubs/realtime/GuildDeleted")]
    [SubscribeOperation(typeof(GuildDeletedEvent),
        Summary = "Received when a guild is deleted.")]
    public void OnGuildDeleted() { }

    [Channel("hubs/realtime/GuildOwnershipTransferred")]
    [SubscribeOperation(typeof(GuildOwnershipTransferredEvent),
        Summary = "Received when a guild's ownership is transferred to another member.")]
    public void OnGuildOwnershipTransferred() { }

    [Channel("hubs/realtime/UserTyping")]
    [SubscribeOperation(typeof(UserTypingEvent),
        Summary = "Received when a user starts typing in a guild text channel.")]
    public void OnUserTyping() { }

    [Channel("hubs/realtime/ConversationUserTyping")]
    [SubscribeOperation(typeof(ConversationUserTypingEvent),
        Summary = "Received when a user starts typing in a direct conversation.")]
    public void OnConversationUserTyping() { }

    [Channel("hubs/realtime/UserPresenceChanged")]
    [SubscribeOperation(typeof(UserPresenceChangedEvent),
        Summary = "Received when a user changes their presence status. Invisible users appear as 'offline'.")]
    public void OnUserPresenceChanged() { }

    [Channel("hubs/realtime/ReactionAdded")]
    [SubscribeOperation(typeof(ReactionAddedEvent),
        Summary = "Received when a user adds an emoji reaction to a message (channel or conversation).")]
    public void OnReactionAdded() { }

    [Channel("hubs/realtime/ReactionRemoved")]
    [SubscribeOperation(typeof(ReactionRemovedEvent),
        Summary = "Received when a user removes an emoji reaction from a message (channel or conversation).")]
    public void OnReactionRemoved() { }
}

// ── Client → Server payload types ─────────────────────────────────

public sealed record StartTypingChannelMessage(Guid ChannelId);
public sealed record StartTypingConversationMessage(Guid ConversationId);
