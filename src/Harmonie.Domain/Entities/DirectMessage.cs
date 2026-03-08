using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class DirectMessage : Entity<DirectMessageId>
{
    public ConversationId ConversationId { get; private set; }

    public UserId AuthorUserId { get; private set; }

    public MessageContent Content { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    private DirectMessage(
        DirectMessageId id,
        ConversationId conversationId,
        UserId authorUserId,
        MessageContent content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        AuthorUserId = authorUserId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
    }

    public static Result<DirectMessage> Create(
        ConversationId conversationId,
        UserId authorUserId,
        MessageContent content)
    {
        if (conversationId is null)
            return Result.Failure<DirectMessage>("Conversation ID is required");

        if (authorUserId is null)
            return Result.Failure<DirectMessage>("Author user ID is required");

        if (content is null)
            return Result.Failure<DirectMessage>("Message content is required");

        return Result.Success(new DirectMessage(
            DirectMessageId.New(),
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

    public static DirectMessage Rehydrate(
        DirectMessageId id,
        ConversationId conversationId,
        UserId authorUserId,
        MessageContent content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(authorUserId);
        ArgumentNullException.ThrowIfNull(content);

        return new DirectMessage(
            id,
            conversationId,
            authorUserId,
            content,
            createdAtUtc,
            updatedAtUtc,
            deletedAtUtc);
    }
}
