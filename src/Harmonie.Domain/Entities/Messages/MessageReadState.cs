using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Messages;

public sealed class MessageReadState
{
    public UserId UserId { get; }

    public GuildChannelId? ChannelId { get; }

    public ConversationId? ConversationId { get; }

    public MessageId LastReadMessageId { get; private set; }

    public DateTime ReadAtUtc { get; private set; }

    private MessageReadState(
        UserId userId,
        GuildChannelId? channelId,
        ConversationId? conversationId,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        UserId = userId;
        ChannelId = channelId;
        ConversationId = conversationId;
        LastReadMessageId = lastReadMessageId;
        ReadAtUtc = readAtUtc;
    }

    public static Result<MessageReadState> CreateForChannel(
        UserId userId,
        GuildChannelId channelId,
        MessageId lastReadMessageId)
    {
        if (channelId is null)
            return Result.Failure<MessageReadState>("Channel ID is required");

        return Create(userId, channelId, conversationId: null, lastReadMessageId);
    }

    public static Result<MessageReadState> CreateForConversation(
        UserId userId,
        ConversationId conversationId,
        MessageId lastReadMessageId)
    {
        if (conversationId is null)
            return Result.Failure<MessageReadState>("Conversation ID is required");

        return Create(userId, channelId: null, conversationId, lastReadMessageId);
    }

    private static Result<MessageReadState> Create(
        UserId userId,
        GuildChannelId? channelId,
        ConversationId? conversationId,
        MessageId lastReadMessageId)
    {
        if (userId is null)
            return Result.Failure<MessageReadState>("User ID is required");

        if (lastReadMessageId is null)
            return Result.Failure<MessageReadState>("Last read message ID is required");

        return Result.Success(new MessageReadState(
            userId, channelId, conversationId, lastReadMessageId, DateTime.UtcNow));
    }

    public static MessageReadState Rehydrate(
        UserId userId,
        GuildChannelId? channelId,
        ConversationId? conversationId,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if ((channelId is null) == (conversationId is null))
            throw new ArgumentException("Exactly one of ChannelId or ConversationId must be set.");

        ArgumentNullException.ThrowIfNull(lastReadMessageId);

        return new MessageReadState(userId, channelId, conversationId, lastReadMessageId, readAtUtc);
    }

    public void Acknowledge(MessageId messageId)
    {
        LastReadMessageId = messageId;
        ReadAtUtc = DateTime.UtcNow;
    }
}
