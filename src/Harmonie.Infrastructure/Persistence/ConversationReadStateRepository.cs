using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Infrastructure.Persistence;

public sealed class ConversationReadStateRepository : IConversationReadStateRepository
{
    private readonly DbSession _dbSession;

    public ConversationReadStateRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task UpsertAsync(
        UserId userId,
        ConversationId conversationId,
        MessageId lastReadMessageId,
        DateTime readAtUtc,
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
                UserId = userId.Value,
                ConversationId = conversationId.Value,
                LastReadMessageId = lastReadMessageId.Value,
                ReadAtUtc = readAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
