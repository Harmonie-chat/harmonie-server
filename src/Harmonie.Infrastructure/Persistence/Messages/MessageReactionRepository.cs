using Dapper;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Messages;

namespace Harmonie.Infrastructure.Persistence.Messages;

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
        MessageReaction reaction,
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
                MessageId = reaction.MessageId.Value,
                UserId = reaction.UserId.Value,
                Emoji = reaction.Emoji,
                CreatedAtUtc = reaction.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task RemoveAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           DELETE FROM message_reactions
                           WHERE message_id = @MessageId
                             AND user_id    = @UserId
                             AND emoji      = @Emoji
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

        await connection.ExecuteAsync(command);
    }

    public async Task<ReactionUsersPage> GetReactionUsersAsync(
        MessageId messageId,
        string emoji,
        int limit,
        ReactionUsersCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;

        var cursorCondition = cursor is not null
            ? "AND (mr.created_at_utc, mr.user_id) > (@CursorCreatedAtUtc, @CursorUserId)"
            : "1=1";

        var sql = $@"
                   SELECT mr.user_id AS ""UserId"",
                          u.username AS ""Username"",
                          u.display_name AS ""DisplayName"",
                          mr.created_at_utc AS ""CreatedAtUtc""
                   FROM message_reactions mr
                   JOIN users u ON u.id = mr.user_id
                   WHERE mr.message_id = @MessageId
                     AND mr.emoji = @Emoji
                     AND {cursorCondition}
                   ORDER BY mr.created_at_utc, mr.user_id
                   LIMIT @Take;

                   SELECT COUNT(*)
                   FROM message_reactions
                   WHERE message_id = @MessageId
                     AND emoji = @Emoji;
                   ";

        var parameters = new DynamicParameters();
        parameters.Add("MessageId", messageId.Value);
        parameters.Add("Emoji", emoji);
        parameters.Add("Take", take);
        if (cursor is not null)
        {
            parameters.Add("CursorCreatedAtUtc", cursor.CreatedAtUtc);
            parameters.Add("CursorUserId", cursor.UserId);
        }

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<ReactionUserDetailRow>()).ToArray();
        var totalCount = (await multi.ReadAsync<int>()).Single();

        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        ReactionUsersCursor? nextCursor = null;
        if (hasMore && pageRows.Length > 0)
        {
            var lastPageRow = pageRows[pageRows.Length - 1];
            nextCursor = new ReactionUsersCursor(lastPageRow.CreatedAtUtc, lastPageRow.UserId);
        }

        var users = pageRows
            .Select(row => new ReactionUser(row.UserId, row.Username, row.DisplayName))
            .ToArray();

        return new ReactionUsersPage(users, totalCount, nextCursor);
    }
}
