using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Infrastructure.Persistence;

public sealed class MessageReactionRepository : IMessageReactionRepository
{
    private readonly DbSession _dbSession;

    public MessageReactionRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<bool> ExistsAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM message_reactions
                               WHERE message_id = @MessageId
                                 AND user_id    = @UserId
                                 AND emoji      = @Emoji
                           )
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = messageId.Value,
                UserId = userId.Value,
                Emoji = emoji
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task AddAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO message_reactions (message_id, user_id, emoji, created_at_utc)
                           VALUES (@MessageId, @UserId, @Emoji, @CreatedAtUtc)
                           ON CONFLICT (message_id, user_id, emoji) DO NOTHING
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = messageId.Value,
                UserId = userId.Value,
                Emoji = emoji,
                CreatedAtUtc = createdAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
