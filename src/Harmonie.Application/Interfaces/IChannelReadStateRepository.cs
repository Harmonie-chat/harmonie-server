using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IChannelReadStateRepository
{
    Task UpsertAsync(
        UserId userId,
        GuildChannelId channelId,
        MessageId lastReadMessageId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);

    Task<MessageId?> GetLastReadMessageIdAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);
}
