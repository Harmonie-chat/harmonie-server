using Dapper;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Messages;

public sealed class PinnedMessageRepository : IPinnedMessageRepository
{
    private readonly DbSession _dbSession;

    public PinnedMessageRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<bool> IsPinnedAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM pinned_messages
                               WHERE message_id = @MessageId
                           )
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task AddAsync(
        PinnedMessage pinnedMessage,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO pinned_messages (message_id, pinned_by_user_id, pinned_at_utc)
                           VALUES (@MessageId, @PinnedByUserId, @PinnedAtUtc)
                           ON CONFLICT (message_id) DO NOTHING
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = pinnedMessage.MessageId.Value,
                PinnedByUserId = pinnedMessage.PinnedByUserId.Value,
                PinnedAtUtc = pinnedMessage.PinnedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task RemoveAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           DELETE FROM pinned_messages
                           WHERE message_id = @MessageId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
