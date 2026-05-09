using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Messages;

public sealed class Message : Entity<MessageId>
{
    public const int MaxMentionedUsers = 50;

    public MessageScope Scope { get; private set; }

    public UserId AuthorUserId { get; private set; }

    public MessageContent? Content { get; private set; }

    public MessageId? ReplyToMessageId { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public IReadOnlyCollection<UserId> MentionedUserIds { get; private set; }

    private Message(
        MessageId id,
        MessageScope scope,
        UserId authorUserId,
        MessageId? replyToMessageId,
        MessageContent? content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc,
        IReadOnlyCollection<UserId> mentionedUserIds)
    {
        Id = id;
        Scope = scope;
        AuthorUserId = authorUserId;
        ReplyToMessageId = replyToMessageId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
        MentionedUserIds = mentionedUserIds;
    }

    public static Result<Message> Create(
        MessageScope scope,
        UserId authorUserId,
        MessageContent? content,
        MessageId? replyToMessageId = null,
        IReadOnlyCollection<UserId>? mentionedUserIds = null)
    {
        if (scope is null)
            return Result.Failure<Message>("Message scope is required");
        if (authorUserId is null)
            return Result.Failure<Message>("Author user ID is required");

        var mentions = mentionedUserIds ?? Array.Empty<UserId>();

        if (mentions.Count > MaxMentionedUsers)
            return Result.Failure<Message>($"A message can mention at most {MaxMentionedUsers} users");

        if (mentions.Distinct().Count() != mentions.Count)
            return Result.Failure<Message>("Mentioned user IDs must be distinct");

        return Result.Success(new Message(
            MessageId.New(),
            scope,
            authorUserId,
            replyToMessageId,
            content,
            DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null,
            mentionedUserIds: mentions.ToArray()));
    }

    public Result UpdateContent(MessageContent newContent)
    {
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
        MessageScope scope,
        UserId authorUserId,
        MessageId? replyToMessageId,
        MessageContent? content,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc,
        IReadOnlyCollection<UserId>? mentionedUserIds = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(authorUserId);

        return new Message(
            id,
            scope,
            authorUserId,
            replyToMessageId,
            content,
            createdAtUtc,
            updatedAtUtc,
            deletedAtUtc,
            mentionedUserIds ?? Array.Empty<UserId>());
    }
}
