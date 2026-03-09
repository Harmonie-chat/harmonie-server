using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class ChannelMessage : Entity<ChannelMessageId>
{
    public GuildChannelId ChannelId { get; private set; }

    public UserId AuthorUserId { get; private set; }

    public MessageContent Content { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    private ChannelMessage(
        ChannelMessageId id,
        GuildChannelId channelId,
        UserId authorUserId,
        MessageContent content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
    {
        Id = id;
        ChannelId = channelId;
        AuthorUserId = authorUserId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
    }

    public static Result<ChannelMessage> Create(
        GuildChannelId channelId,
        UserId authorUserId,
        MessageContent content)
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
            DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null));
    }

    public Result UpdateContent(MessageContent newContent)
    {
        if (newContent is null)
            return Result.Failure("New content is required");

        Content = newContent;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Delete()
    {
        if (DeletedAtUtc is not null)
            return Result.Failure("Message is already deleted");

        DeletedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
        return Result.Success();
    }

    public static ChannelMessage Rehydrate(
        ChannelMessageId id,
        GuildChannelId channelId,
        UserId authorUserId,
        MessageContent content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
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
            createdAtUtc,
            updatedAtUtc,
            deletedAtUtc);
    }
}
