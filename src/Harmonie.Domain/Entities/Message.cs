using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class Message : Entity<MessageId>
{
    public GuildChannelId? ChannelId { get; private set; }

    public ConversationId? ConversationId { get; private set; }

    public UserId AuthorUserId { get; private set; }

    public MessageContent Content { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    private Message(
        MessageId id,
        GuildChannelId? channelId,
        ConversationId? conversationId,
        UserId authorUserId,
        MessageContent content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
    {
        Id = id;
        ChannelId = channelId;
        ConversationId = conversationId;
        AuthorUserId = authorUserId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
    }

    public static Result<Message> CreateForChannel(
        GuildChannelId channelId,
        UserId authorUserId,
        MessageContent content)
    {
        if (channelId is null)
            return Result.Failure<Message>("Channel ID is required");
        if (authorUserId is null)
            return Result.Failure<Message>("Author user ID is required");
        if (content is null)
            return Result.Failure<Message>("Message content is required");

        return Result.Success(new Message(
            MessageId.New(),
            channelId,
            conversationId: null,
            authorUserId,
            content,
            DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null));
    }

    public static Result<Message> CreateForConversation(
        ConversationId conversationId,
        UserId authorUserId,
        MessageContent content)
    {
        if (conversationId is null)
            return Result.Failure<Message>("Conversation ID is required");
        if (authorUserId is null)
            return Result.Failure<Message>("Author user ID is required");
        if (content is null)
            return Result.Failure<Message>("Message content is required");

        return Result.Success(new Message(
            MessageId.New(),
            channelId: null,
            conversationId,
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

    public static Message Rehydrate(
        MessageId id,
        GuildChannelId? channelId,
        ConversationId? conversationId,
        UserId authorUserId,
        MessageContent content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(authorUserId);
        ArgumentNullException.ThrowIfNull(content);

        if ((channelId is null) == (conversationId is null))
            throw new ArgumentException("Exactly one parent reference is required.", nameof(channelId));

        return new Message(
            id,
            channelId,
            conversationId,
            authorUserId,
            content,
            createdAtUtc,
            updatedAtUtc,
            deletedAtUtc);
    }
}
