using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Messages;

public sealed class Message : Entity<MessageId>
{
    public GuildChannelId? ChannelId { get; private set; }

    public ConversationId? ConversationId { get; private set; }

    public UserId AuthorUserId { get; private set; }

    public MessageContent Content { get; private set; }

    public IReadOnlyList<MessageAttachment> Attachments { get; private set; } = Array.Empty<MessageAttachment>();

    public DateTime? DeletedAtUtc { get; private set; }

    private Message(
        MessageId id,
        GuildChannelId? channelId,
        ConversationId? conversationId,
        UserId authorUserId,
        MessageContent content,
        IReadOnlyList<MessageAttachment> attachments,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc)
    {
        Id = id;
        ChannelId = channelId;
        ConversationId = conversationId;
        AuthorUserId = authorUserId;
        Content = content;
        Attachments = attachments.ToArray();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
    }

    public static Result<Message> CreateForChannel(
        GuildChannelId channelId,
        UserId authorUserId,
        MessageContent content,
        IReadOnlyList<MessageAttachment>? attachments = null)
    {
        if (channelId is null)
            return Result.Failure<Message>("Channel ID is required");
        if (authorUserId is null)
            return Result.Failure<Message>("Author user ID is required");
        if (content is null)
            return Result.Failure<Message>("Message content is required");
        if (attachments is null)
            attachments = Array.Empty<MessageAttachment>();
        if (attachments.Any(attachment => attachment is null))
            return Result.Failure<Message>("Message attachments are invalid");
        if (content.Value.Length == 0 && attachments.Count == 0)
            return Result.Failure<Message>("Message must have content or at least one attachment");

        return Result.Success(new Message(
            MessageId.New(),
            channelId,
            conversationId: null,
            authorUserId,
            content,
            attachments,
            DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null));
    }

    public static Result<Message> CreateForConversation(
        ConversationId conversationId,
        UserId authorUserId,
        MessageContent content,
        IReadOnlyList<MessageAttachment>? attachments = null)
    {
        if (conversationId is null)
            return Result.Failure<Message>("Conversation ID is required");
        if (authorUserId is null)
            return Result.Failure<Message>("Author user ID is required");
        if (content is null)
            return Result.Failure<Message>("Message content is required");
        if (attachments is null)
            attachments = Array.Empty<MessageAttachment>();
        if (attachments.Any(attachment => attachment is null))
            return Result.Failure<Message>("Message attachments are invalid");
        if (content.Value.Length == 0 && attachments.Count == 0)
            return Result.Failure<Message>("Message must have content or at least one attachment");

        return Result.Success(new Message(
            MessageId.New(),
            channelId: null,
            conversationId,
            authorUserId,
            content,
            attachments,
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

    public Result RemoveAttachment(UploadedFileId attachmentFileId)
    {
        if (attachmentFileId is null)
            return Result.Failure("Attachment file ID is required");

        var remainingAttachments = Attachments
            .Where(attachment => attachment.FileId != attachmentFileId)
            .ToArray();

        if (remainingAttachments.Length == Attachments.Count)
            return Result.Failure("Attachment was not found on message");

        Attachments = remainingAttachments;
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
        DateTime? deletedAtUtc,
        IReadOnlyList<MessageAttachment>? attachments = null)
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
            attachments ?? Array.Empty<MessageAttachment>(),
            createdAtUtc,
            updatedAtUtc,
            deletedAtUtc);
    }
}
