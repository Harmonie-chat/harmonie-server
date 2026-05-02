using Dapper;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Channels;

public sealed class ChannelReadStateRepository : IChannelReadStateRepository
{
    private readonly DbSession _dbSession;

    public ChannelReadStateRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task UpsertAsync(
        MessageReadState state,
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
                UserId = state.UserId.Value,
                ChannelId = state.ChannelId!.Value,
                LastReadMessageId = state.LastReadMessageId.Value,
                ReadAtUtc = state.ReadAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<MessageReadState?> GetAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT user_id             AS "UserId",
                                  channel_id          AS "ChannelId",
                                  last_read_message_id AS "LastReadMessageId",
                                  read_at_utc         AS "ReadAtUtc"
                           FROM channel_read_states
                           WHERE user_id    = @UserId
                             AND channel_id = @ChannelId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId.Value, ChannelId = channelId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<Row>(command);
        return row is null ? null : MessageReadState.Rehydrate(
            UserId.From(row.UserId),
            GuildChannelId.From(row.ChannelId),
            conversationId: null,
            MessageId.From(row.LastReadMessageId),
            row.ReadAtUtc);
    }

    private sealed class Row
    {
        public Guid UserId { get; init; }
        public Guid ChannelId { get; init; }
        public Guid LastReadMessageId { get; init; }
        public DateTime ReadAtUtc { get; init; }
    }
}
