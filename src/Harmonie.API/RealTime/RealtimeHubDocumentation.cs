using Saunter.Attributes;

namespace Harmonie.API.RealTime;

/// <summary>
/// AsyncAPI documentation for the RealtimeHub SignalR hub.
/// This class is not invoked at runtime — it exists solely to generate
/// the AsyncAPI specification via Saunter's attribute scanning.
/// </summary>
[AsyncApi]
public class RealtimeHubDocumentation
{
    // ── Client → Server (Publish) ──────────────────────────────────

    [Channel("hubs/realtime/JoinChannel")]
    [PublishOperation(typeof(JoinChannelMessage),
        Summary = "Join a text channel group to receive real-time messages.")]
    public void JoinChannel() { }

    [Channel("hubs/realtime/LeaveChannel")]
    [PublishOperation(typeof(LeaveChannelMessage),
        Summary = "Leave a text channel group.")]
    public void LeaveChannel() { }

    [Channel("hubs/realtime/JoinGuild")]
    [PublishOperation(typeof(JoinGuildMessage),
        Summary = "Join a guild group to receive voice presence events.")]
    public void JoinGuild() { }

    [Channel("hubs/realtime/LeaveGuild")]
    [PublishOperation(typeof(LeaveGuildMessage),
        Summary = "Leave a guild group.")]
    public void LeaveGuild() { }

    [Channel("hubs/realtime/JoinConversation")]
    [PublishOperation(typeof(JoinConversationMessage),
        Summary = "Join a direct conversation group to receive real-time messages.")]
    public void JoinConversation() { }

    [Channel("hubs/realtime/LeaveConversation")]
    [PublishOperation(typeof(LeaveConversationMessage),
        Summary = "Leave a direct conversation group.")]
    public void LeaveConversation() { }

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

    [Channel("hubs/realtime/GuildDeleted")]
    [SubscribeOperation(typeof(GuildDeletedEvent),
        Summary = "Received when a guild is deleted.")]
    public void OnGuildDeleted() { }

    [Channel("hubs/realtime/UserTyping")]
    [SubscribeOperation(typeof(UserTypingEvent),
        Summary = "Received when a user starts typing in a guild text channel.")]
    public void OnUserTyping() { }

    [Channel("hubs/realtime/ConversationUserTyping")]
    [SubscribeOperation(typeof(ConversationUserTypingEvent),
        Summary = "Received when a user starts typing in a direct conversation.")]
    public void OnConversationUserTyping() { }
}

// ── Client → Server payload types ─────────────────────────────────

public sealed record JoinChannelMessage(Guid ChannelId);
public sealed record LeaveChannelMessage(Guid ChannelId);
public sealed record JoinGuildMessage(Guid GuildId);
public sealed record LeaveGuildMessage(Guid GuildId);
public sealed record JoinConversationMessage(Guid ConversationId);
public sealed record LeaveConversationMessage(Guid ConversationId);
public sealed record StartTypingChannelMessage(Guid ChannelId);
public sealed record StartTypingConversationMessage(Guid ConversationId);
