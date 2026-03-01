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

    Task<ChannelMessage?> GetByIdAsync(
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ChannelMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record ChannelMessageCursor(
    DateTime CreatedAtUtc,
    ChannelMessageId MessageId);

public sealed record ChannelMessagePage(
    IReadOnlyList<ChannelMessage> Items,
    ChannelMessageCursor? NextCursor);
