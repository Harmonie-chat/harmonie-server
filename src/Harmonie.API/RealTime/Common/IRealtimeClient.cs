using Harmonie.API.RealTime.Channels;
using Harmonie.API.RealTime.Conversations;
using Harmonie.API.RealTime.Guilds;
using Harmonie.API.RealTime.Messages;
using Harmonie.API.RealTime.Users;
using Harmonie.API.RealTime.Voice;

namespace Harmonie.API.RealTime.Common;

public interface IRealtimeClient
{
    // Lifecycle
    Task Ready(CancellationToken cancellationToken = default);

    // Typing
    Task UserTyping(UserTypingEvent payload, CancellationToken cancellationToken = default);
    Task ConversationUserTyping(ConversationUserTypingEvent payload, CancellationToken cancellationToken = default);

    // Messages (channels)
    Task MessageCreated(MessageCreatedEvent payload, CancellationToken cancellationToken = default);
    Task MessageUpdated(MessageUpdatedEvent payload, CancellationToken cancellationToken = default);
    Task MessageDeleted(MessageDeletedEvent payload, CancellationToken cancellationToken = default);

    // Conversations
    Task ConversationCreated(ConversationCreatedEvent payload, CancellationToken cancellationToken = default);
    Task ConversationParticipantLeft(ConversationParticipantLeftEvent payload, CancellationToken cancellationToken = default);
    Task ConversationMessageCreated(ConversationMessageCreatedEvent payload, CancellationToken cancellationToken = default);
    Task ConversationMessageUpdated(ConversationMessageUpdatedEvent payload, CancellationToken cancellationToken = default);
    Task ConversationMessageDeleted(ConversationMessageDeletedEvent payload, CancellationToken cancellationToken = default);

    // Voice
    Task VoiceParticipantJoined(VoiceParticipantJoinedEvent payload, CancellationToken cancellationToken = default);
    Task VoiceParticipantLeft(VoiceParticipantLeftEvent payload, CancellationToken cancellationToken = default);

    // Guilds
    Task GuildDeleted(GuildDeletedEvent payload, CancellationToken cancellationToken = default);
    Task GuildOwnershipTransferred(GuildOwnershipTransferredEvent payload, CancellationToken cancellationToken = default);
    Task GuildUpdated(GuildUpdatedEvent payload, CancellationToken cancellationToken = default);

    // Guild channels
    Task ChannelCreated(ChannelCreatedEvent payload, CancellationToken cancellationToken = default);
    Task ChannelUpdated(ChannelUpdatedEvent payload, CancellationToken cancellationToken = default);
    Task ChannelDeleted(ChannelDeletedEvent payload, CancellationToken cancellationToken = default);
    Task ChannelsReordered(ChannelsReorderedEvent payload, CancellationToken cancellationToken = default);

    // Members
    Task MemberJoined(MemberJoinedEvent payload, CancellationToken cancellationToken = default);
    Task MemberLeft(MemberLeftEvent payload, CancellationToken cancellationToken = default);
    Task MemberBanned(MemberBannedEvent payload, CancellationToken cancellationToken = default);
    Task YouWereBanned(YouWereBannedEvent payload, CancellationToken cancellationToken = default);
    Task MemberRemoved(MemberRemovedEvent payload, CancellationToken cancellationToken = default);
    Task YouWereKicked(YouWereKickedEvent payload, CancellationToken cancellationToken = default);
    Task MemberRoleUpdated(MemberRoleUpdatedEvent payload, CancellationToken cancellationToken = default);

    // Users
    Task UserPresenceChanged(UserPresenceChangedEvent payload, CancellationToken cancellationToken = default);
    Task UserProfileUpdated(UserProfileUpdatedEvent payload, CancellationToken cancellationToken = default);

    // Reactions
    Task ReactionAdded(ReactionAddedEvent payload, CancellationToken cancellationToken = default);
    Task ReactionRemoved(ReactionRemovedEvent payload, CancellationToken cancellationToken = default);
}
