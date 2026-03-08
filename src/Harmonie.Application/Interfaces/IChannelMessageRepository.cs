using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IChannelMessageRepository
{
    Task AddAsync(
        ChannelMessage message,
        CancellationToken cancellationToken = default);

    Task<ChannelMessagePage> GetPageAsync(
        GuildChannelId channelId,
        ChannelMessageCursor? beforeCursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<SearchGuildMessagesPage> SearchGuildMessagesAsync(
        SearchGuildMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ChannelMessage?> GetByIdAsync(
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ChannelMessage message,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default);
}

public sealed record ChannelMessageCursor(
    DateTime CreatedAtUtc,
    ChannelMessageId MessageId);

public sealed record ChannelMessagePage(
    IReadOnlyList<ChannelMessage> Items,
    ChannelMessageCursor? NextCursor);

public sealed record SearchGuildMessagesQuery(
    GuildId GuildId,
    string SearchText,
    GuildChannelId? ChannelId,
    UserId? AuthorId,
    DateTime? BeforeCreatedAtUtc,
    DateTime? AfterCreatedAtUtc,
    ChannelMessageCursor? Cursor);

public sealed record SearchGuildMessagesItem(
    ChannelMessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    MessageContent Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SearchGuildMessagesPage(
    IReadOnlyList<SearchGuildMessagesItem> Items,
    ChannelMessageCursor? NextCursor);
