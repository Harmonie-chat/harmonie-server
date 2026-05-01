using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Messages;

public sealed class MessageReaction
{
    public MessageId MessageId { get; }

    public UserId UserId { get; }

    public string Emoji { get; }

    public DateTime CreatedAtUtc { get; }

    private MessageReaction(
        MessageId messageId,
        UserId userId,
        string emoji,
        DateTime createdAtUtc)
    {
        MessageId = messageId;
        UserId = userId;
        Emoji = emoji;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<MessageReaction> Create(
        MessageId messageId,
        UserId userId,
        string emoji)
    {
        if (messageId is null)
            return Result.Failure<MessageReaction>("Message ID is required");

        if (userId is null)
            return Result.Failure<MessageReaction>("User ID is required");

        if (string.IsNullOrWhiteSpace(emoji))
            return Result.Failure<MessageReaction>("Emoji is required");

        return Result.Success(new MessageReaction(
            messageId,
            userId,
            emoji,
            DateTime.UtcNow));
    }

    public static MessageReaction Rehydrate(
        MessageId messageId,
        UserId userId,
        string emoji,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emoji);

        return new MessageReaction(messageId, userId, emoji, createdAtUtc);
    }
}
