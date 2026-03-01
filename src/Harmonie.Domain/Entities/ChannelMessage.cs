using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class ChannelMessage : Entity<ChannelMessageId>
{
    public GuildChannelId ChannelId { get; private set; }

    public UserId AuthorUserId { get; private set; }

    public ChannelMessageContent Content { get; private set; }

    private ChannelMessage(
        ChannelMessageId id,
        GuildChannelId channelId,
        UserId authorUserId,
        ChannelMessageContent content,
        DateTime createdAtUtc)
    {
        Id = id;
        ChannelId = channelId;
        AuthorUserId = authorUserId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<ChannelMessage> Create(
        GuildChannelId channelId,
        UserId authorUserId,
        ChannelMessageContent content)
    {
        if (channelId is null)
            return Result.Failure<ChannelMessage>("Channel ID is required");
        if (authorUserId is null)
            return Result.Failure<ChannelMessage>("Author user ID is required");
        if (content is null)
            return Result.Failure<ChannelMessage>("Message content is required");

        return Result.Success(new ChannelMessage(
            ChannelMessageId.New(),
            channelId,
            authorUserId,
            content,
            DateTime.UtcNow));
    }

    public Result UpdateContent(ChannelMessageContent newContent)
    {
        if (newContent is null)
            return Result.Failure("New content is required");

        Content = newContent;
        return Result.Success();
    }

    public static ChannelMessage Rehydrate(
        ChannelMessageId id,
        GuildChannelId channelId,
        UserId authorUserId,
        ChannelMessageContent content,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(channelId);
        ArgumentNullException.ThrowIfNull(authorUserId);
        ArgumentNullException.ThrowIfNull(content);

        return new ChannelMessage(
            id,
            channelId,
            authorUserId,
            content,
            createdAtUtc);
    }
}
