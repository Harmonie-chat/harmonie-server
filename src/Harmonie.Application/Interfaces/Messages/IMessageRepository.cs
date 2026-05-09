using Harmonie.Application.Common.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;


namespace Harmonie.Application.Interfaces.Messages;

public sealed record ReplyTargetSummary(
    MessageId MessageId,
    MessageScope Scope,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    bool HasAttachments,
    bool IsDeleted,
    DateTime? DeletedAtUtc);

public interface IMessageRepository
{
    Task AddAsync(
        Message message,
        CancellationToken cancellationToken = default);

    Task<Message?> GetByIdAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Message message,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(
        Message message,
        CancellationToken cancellationToken = default);

    Task<MessageId?> GetLatestChannelMessageIdAsync(
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);

    Task<MessageId?> GetLatestConversationMessageIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);

    Task<ReplyTargetSummary?> GetReplyTargetSummaryAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);

    Task<int> SoftDeleteByAuthorInGuildAsync(
        GuildId guildId,
        UserId authorUserId,
        int days,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist mention rows for a message. Must be called inside the same transaction as the message insert.
    /// </summary>
    Task AddMentionsAsync(
        MessageId messageId,
        IReadOnlyCollection<UserId> mentionedUserIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace the mention set for a message atomically (delete all existing, insert new).
    /// Must be called inside a transaction.
    /// </summary>
    Task ReplaceMentionsAsync(
        MessageId messageId,
        IReadOnlyCollection<UserId> mentionedUserIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get mentioned user IDs for a set of messages. Returns empty for messages without mentions.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetMentionedUserIdsByMessageIdAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default);
}

public sealed record MessageCursor(
    DateTime CreatedAtUtc,
    MessageId MessageId);

public sealed record MessagePage(
    IReadOnlyList<Message> Items,
    MessageCursor? NextCursor,
    IReadOnlyDictionary<Guid, IReadOnlyList<MessageReactionSummary>> ReactionsByMessageId,
    IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> AttachmentsByMessageId,
    IReadOnlyDictionary<Guid, IReadOnlyList<LinkPreviewDto>>? LinkPreviewsByMessageId = null,
    IReadOnlySet<Guid>? PinnedMessageIds = null,
    IReadOnlyDictionary<Guid, ReplyPreviewDto>? ReplyPreviewsByTargetMessageId = null,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>? MentionedUserIdsByMessageId = null,
    MessageReadState? LastReadState = null);

public sealed record SearchGuildMessagesQuery(
    GuildId GuildId,
    string SearchText,
    GuildChannelId? ChannelId,
    UserId? AuthorId,
    DateTime? BeforeCreatedAtUtc,
    DateTime? AfterCreatedAtUtc,
    MessageCursor? Cursor);

public sealed record SearchGuildMessagesItem(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    UploadedFileId? AuthorAvatarFileId,
    string? AuthorAvatarColor,
    string? AuthorAvatarIcon,
    string? AuthorAvatarBg,
    IReadOnlyList<MessageAttachment> Attachments,
    MessageContent? Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SearchGuildMessagesPage(
    IReadOnlyList<SearchGuildMessagesItem> Items,
    MessageCursor? NextCursor);

public sealed record SearchConversationMessagesQuery(
    ConversationId ConversationId,
    string SearchText,
    DateTime? BeforeCreatedAtUtc,
    DateTime? AfterCreatedAtUtc,
    MessageCursor? Cursor);

public sealed record SearchConversationMessagesItem(
    MessageId MessageId,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    UploadedFileId? AuthorAvatarFileId,
    string? AuthorAvatarColor,
    string? AuthorAvatarIcon,
    string? AuthorAvatarBg,
    IReadOnlyList<MessageAttachment> Attachments,
    MessageContent? Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SearchConversationMessagesPage(
    IReadOnlyList<SearchConversationMessagesItem> Items,
    MessageCursor? NextCursor);
