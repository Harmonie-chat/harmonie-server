using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;

namespace Harmonie.Domain.ValueObjects.Messages;

/// <summary>
/// Discriminated union for the parent of a message or read state.
/// Guarantees at the type level that exactly one parent is set.
/// </summary>
public abstract record MessageScope
{
    private MessageScope() { }

    public sealed record Channel(GuildChannelId ChannelId) : MessageScope;

    public sealed record Conversation(ConversationId ConversationId) : MessageScope;

    public bool Matches(GuildChannelId channelId) => this is Channel c && c.ChannelId == channelId;

    public bool Matches(ConversationId conversationId) => this is Conversation c && c.ConversationId == conversationId;
}
