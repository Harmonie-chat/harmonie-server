using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Infrastructure.Persistence;

public sealed class ChannelReadStateRepository : IChannelReadStateRepository
{
    private readonly DbSession _dbSession;

    public ChannelReadStateRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task UpsertAsync(
        UserId userId,
        GuildChannelId channelId,
        MessageId lastReadMessageId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO channel_read_states (user_id, channel_id, last_read_message_id, read_at_utc)
                           VALUES (@UserId, @ChannelId, @LastReadMessageId, @ReadAtUtc)
                           ON CONFLICT (user_id, channel_id)
                           DO UPDATE SET
                               last_read_message_id = @LastReadMessageId,
                               read_at_utc          = @ReadAtUtc
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                UserId = userId.Value,
                ChannelId = channelId.Value,
                LastReadMessageId = lastReadMessageId.Value,
                ReadAtUtc = readAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<MessageId?> GetLastReadMessageIdAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT last_read_message_id
                           FROM channel_read_states
                           WHERE user_id    = @UserId
                             AND channel_id = @ChannelId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                UserId = userId.Value,
                ChannelId = channelId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QuerySingleOrDefaultAsync<Guid?>(command);
        return result.HasValue ? MessageId.From(result.Value) : null;
    }
}
