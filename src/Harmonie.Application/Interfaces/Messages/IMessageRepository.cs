using Harmonie.Application.Common.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Messages;

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

    Task RemoveAttachmentAsync(
        MessageId messageId,
        UploadedFileId attachmentFileId,
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

    Task<int> SoftDeleteByAuthorInGuildAsync(
        GuildId guildId,
        UserId authorUserId,
        int days,
        CancellationToken cancellationToken = default);
}

public sealed record MessageCursor(
    DateTime CreatedAtUtc,
    MessageId MessageId);

public sealed record MessagePage(
    IReadOnlyList<Message> Items,
    MessageCursor? NextCursor,
    IReadOnlyDictionary<Guid, IReadOnlyList<MessageReactionSummary>> ReactionsByMessageId,
    IReadOnlyDictionary<Guid, IReadOnlyList<LinkPreviewDto>>? LinkPreviewsByMessageId = null,
    IReadOnlySet<Guid>? PinnedMessageIds = null,
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
