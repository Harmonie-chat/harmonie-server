using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IMessageRepository
{
    Task AddAsync(
        Message message,
        CancellationToken cancellationToken = default);

    Task<MessagePage> GetChannelPageAsync(
        GuildChannelId channelId,
        MessageCursor? beforeCursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<MessagePage> GetConversationPageAsync(
        ConversationId conversationId,
        MessageCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<SearchGuildMessagesPage> SearchGuildMessagesAsync(
        SearchGuildMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<SearchConversationMessagesPage> SearchConversationMessagesAsync(
        SearchConversationMessagesQuery query,
        int limit,
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
}

public sealed record MessageCursor(
    DateTime CreatedAtUtc,
    MessageId MessageId);

public sealed record MessagePage(
    IReadOnlyList<Message> Items,
    MessageCursor? NextCursor);

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
    MessageContent Content,
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
    MessageContent Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SearchConversationMessagesPage(
    IReadOnlyList<SearchConversationMessagesItem> Items,
    MessageCursor? NextCursor);
