using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Conversations;

public sealed class ConversationReadState
{
    public UserId UserId { get; }

    public ConversationId ConversationId { get; }

    public MessageId LastReadMessageId { get; private set; }

    public DateTime ReadAtUtc { get; private set; }

    private ConversationReadState(
        UserId userId,
        ConversationId conversationId,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        UserId = userId;
        ConversationId = conversationId;
        LastReadMessageId = lastReadMessageId;
        ReadAtUtc = readAtUtc;
    }

    public static Result<ConversationReadState> Create(
        UserId userId,
        ConversationId conversationId,
        MessageId lastReadMessageId)
    {
        if (userId is null)
            return Result.Failure<ConversationReadState>("User ID is required");

        if (conversationId is null)
            return Result.Failure<ConversationReadState>("Conversation ID is required");

        if (lastReadMessageId is null)
            return Result.Failure<ConversationReadState>("Last read message ID is required");

        return Result.Success(new ConversationReadState(
            userId,
            conversationId,
            lastReadMessageId,
            DateTime.UtcNow));
    }

    public static ConversationReadState Rehydrate(
        UserId userId,
        ConversationId conversationId,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(lastReadMessageId);

        return new ConversationReadState(userId, conversationId, lastReadMessageId, readAtUtc);
    }

    public void Acknowledge(MessageId messageId, DateTime readAtUtc)
    {
        LastReadMessageId = messageId;
        ReadAtUtc = readAtUtc;
    }
}
