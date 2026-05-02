using Dapper;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Conversations;

public sealed class ConversationReadStateRepository : IConversationReadStateRepository
{
    private readonly DbSession _dbSession;

    public ConversationReadStateRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task UpsertAsync(
        ConversationReadState state,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO conversation_read_states (user_id, conversation_id, last_read_message_id, read_at_utc)
                           VALUES (@UserId, @ConversationId, @LastReadMessageId, @ReadAtUtc)
                           ON CONFLICT (user_id, conversation_id)
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
                ConversationId = state.ConversationId.Value,
                LastReadMessageId = state.LastReadMessageId.Value,
                ReadAtUtc = state.ReadAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<ConversationReadState?> GetAsync(
        UserId userId,
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT user_id             AS "UserId",
                                  conversation_id     AS "ConversationId",
                                  last_read_message_id AS "LastReadMessageId",
                                  read_at_utc         AS "ReadAtUtc"
                           FROM conversation_read_states
                           WHERE user_id          = @UserId
                             AND conversation_id = @ConversationId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId.Value, ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ConversationReadStateRow>(command);
        return row is null ? null : ConversationReadState.Rehydrate(
            UserId.From(row.UserId),
            ConversationId.From(row.ConversationId),
            MessageId.From(row.LastReadMessageId),
            row.ReadAtUtc);
    }

    private sealed class ConversationReadStateRow
    {
        public Guid UserId { get; init; }
        public Guid ConversationId { get; init; }
        public Guid LastReadMessageId { get; init; }
        public DateTime ReadAtUtc { get; init; }
    }
}
