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
        DateTime createdAtUtc,
        MessageId? replyToMessageId = null,
        IReadOnlyCollection<UserId>? mentionedUserIds = null)
    {
        if (scope is null)
            return Result.Failure<Message>("Message scope is required");
        if (authorUserId is null)
            return Result.Failure<Message>("Author user ID is required");

        var mentions = mentionedUserIds ?? Array.Empty<UserId>();
        var validationError = ValidateMentions(mentions);
        if (validationError is not null)
            return Result.Failure<Message>(validationError);

        return Result.Success(new Message(
            MessageId.New(),
            scope,
            authorUserId,
            replyToMessageId,
            content,
            createdAtUtc,
            updatedAtUtc: null,
            deletedAtUtc: null,
            mentionedUserIds: mentions.ToArray()));
    }

    public Result UpdateContent(MessageContent newContent, DateTime updatedAtUtc)
    {
        Content = newContent;
        MarkAsUpdated(updatedAtUtc);
        return Result.Success();
    }

    /// <summary>
    /// Replaces the mention set and marks the entity as updated.
    /// Enforces domain invariants: distinct IDs, max count.
    /// Pass null or empty to clear all mentions.
    /// </summary>
    public Result ReplaceMentions(IReadOnlyCollection<UserId>? mentionedUserIds, DateTime updatedAtUtc)
    {
        var mentions = mentionedUserIds ?? Array.Empty<UserId>();
        var validationError = ValidateMentions(mentions);
        if (validationError is not null)
            return Result.Failure(validationError);

        MentionedUserIds = mentions.ToArray();
        MarkAsUpdated(updatedAtUtc);
        return Result.Success();
    }

    private static string? ValidateMentions(IReadOnlyCollection<UserId> mentions)
    {
        if (mentions.Count > MaxMentionedUsers)
            return $"A message can mention at most {MaxMentionedUsers} users";

        if (new HashSet<UserId>(mentions).Count != mentions.Count)
            return "Mentioned user IDs must be distinct";

        return null;
    }

    public Result Delete(DateTime deletedAtUtc)
    {
        if (DeletedAtUtc is not null)
            return Result.Failure("Message is already deleted");

        DeletedAtUtc = deletedAtUtc;
        MarkAsUpdated(deletedAtUtc);
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
            (mentionedUserIds is not null ? mentionedUserIds.ToArray() : Array.Empty<UserId>()));
    }
}
