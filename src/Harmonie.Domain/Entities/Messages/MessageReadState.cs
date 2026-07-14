using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Messages;

public sealed class MessageReadState
{
    public UserId UserId { get; }

    public MessageScope Scope { get; }

    public MessageId LastReadMessageId { get; private set; }

    public DateTime ReadAtUtc { get; private set; }

    private MessageReadState(
        UserId userId,
        MessageScope scope,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        UserId = userId;
        Scope = scope;
        LastReadMessageId = lastReadMessageId;
        ReadAtUtc = readAtUtc;
    }

    public static Result<MessageReadState> Create(
        UserId userId,
        MessageScope scope,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        if (userId is null)
            return Result.Failure<MessageReadState>("User ID is required");

        if (scope is null)
            return Result.Failure<MessageReadState>("Message scope is required");

        if (lastReadMessageId is null)
            return Result.Failure<MessageReadState>("Last read message ID is required");

        return Result.Success(new MessageReadState(
            userId, scope, lastReadMessageId, readAtUtc));
    }

    public static MessageReadState Rehydrate(
        UserId userId,
        MessageScope scope,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(lastReadMessageId);

        return new MessageReadState(userId, scope, lastReadMessageId, readAtUtc);
    }

    public void Acknowledge(MessageId messageId, DateTime readAtUtc)
    {
        LastReadMessageId = messageId;
        ReadAtUtc = readAtUtc;
    }
}
