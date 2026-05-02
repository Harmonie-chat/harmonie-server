using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Channels;

public sealed class ChannelReadState
{
    public UserId UserId { get; }

    public GuildChannelId ChannelId { get; }

    public MessageId LastReadMessageId { get; private set; }

    public DateTime ReadAtUtc { get; private set; }

    private ChannelReadState(
        UserId userId,
        GuildChannelId channelId,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        UserId = userId;
        ChannelId = channelId;
        LastReadMessageId = lastReadMessageId;
        ReadAtUtc = readAtUtc;
    }

    public static Result<ChannelReadState> Create(
        UserId userId,
        GuildChannelId channelId,
        MessageId lastReadMessageId)
    {
        if (userId is null)
            return Result.Failure<ChannelReadState>("User ID is required");

        if (channelId is null)
            return Result.Failure<ChannelReadState>("Channel ID is required");

        if (lastReadMessageId is null)
            return Result.Failure<ChannelReadState>("Last read message ID is required");

        return Result.Success(new ChannelReadState(
            userId,
            channelId,
            lastReadMessageId,
            DateTime.UtcNow));
    }

    public static ChannelReadState Rehydrate(
        UserId userId,
        GuildChannelId channelId,
        MessageId lastReadMessageId,
        DateTime readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(channelId);
        ArgumentNullException.ThrowIfNull(lastReadMessageId);

        return new ChannelReadState(userId, channelId, lastReadMessageId, readAtUtc);
    }

    public void Acknowledge(MessageId messageId, DateTime readAtUtc)
    {
        LastReadMessageId = messageId;
        ReadAtUtc = readAtUtc;
    }
}
