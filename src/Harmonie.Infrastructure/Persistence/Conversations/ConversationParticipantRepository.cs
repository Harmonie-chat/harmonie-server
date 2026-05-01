using Dapper;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Conversations;

namespace Harmonie.Infrastructure.Persistence.Conversations;

public sealed class ConversationParticipantRepository : IConversationParticipantRepository
{
    private readonly DbSession _dbSession;

    public ConversationParticipantRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<bool> TryAddAsync(
        ConversationParticipant participant,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO conversation_participants (conversation_id, user_id, joined_at_utc)
                           VALUES (@ConversationId, @UserId, @JoinedAtUtc)
                           ON CONFLICT (conversation_id, user_id) DO NOTHING
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                ConversationId = participant.ConversationId.Value,
                UserId = participant.UserId.Value,
                JoinedAtUtc = participant.JoinedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<ConversationParticipant?> GetAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT conversation_id AS "ConversationId",
                                  user_id         AS "UserId",
                                  joined_at_utc   AS "JoinedAtUtc",
                                  hidden_at_utc   AS "HiddenAtUtc"
                           FROM conversation_participants
                           WHERE conversation_id = @ConversationId AND user_id = @UserId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ConversationId = conversationId.Value, UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ConversationParticipantRow>(command);
        return row is null ? null : MapToParticipant(row);
    }

    public async Task<IReadOnlyList<ConversationParticipant>> GetByConversationIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT conversation_id AS "ConversationId",
                                  user_id         AS "UserId",
                                  joined_at_utc   AS "JoinedAtUtc",
                                  hidden_at_utc   AS "HiddenAtUtc"
                           FROM conversation_participants
                           WHERE conversation_id = @ConversationId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<ConversationParticipantRow>(command);
        return rows.Select(MapToParticipant).ToArray();
    }

    public async Task UpdateAsync(
        ConversationParticipant participant,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string sql = """
                            UPDATE conversation_participants
                            SET hidden_at_utc = @HiddenAtUtc
                            WHERE conversation_id = @ConversationId AND user_id = @UserId
                            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                ConversationId = participant.ConversationId.Value,
                UserId = participant.UserId.Value,
                HiddenAtUtc = participant.HiddenAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task UpdateRangeAsync(
        IReadOnlyList<ConversationParticipant> participants,
        CancellationToken cancellationToken = default)
    {
        if (participants.Count == 0)
            return;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string sql = """
                            UPDATE conversation_participants
                            SET hidden_at_utc = @HiddenAtUtc
                            WHERE conversation_id = @ConversationId AND user_id = ANY(@UserIds)
                            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                ConversationId = participants[0].ConversationId.Value,
                UserIds = participants.Select(p => p.UserId.Value).ToArray(),
                HiddenAtUtc = participants[0].HiddenAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<int> RemoveAsync(
        ConversationParticipant participant,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string deleteSql = """
                                  DELETE FROM conversation_participants
                                  WHERE conversation_id = @ConversationId AND user_id = @UserId
                                  """;
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { ConversationId = participant.ConversationId.Value, UserId = participant.UserId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        const string countSql = """
                                 SELECT COUNT(*) FROM conversation_participants
                                 WHERE conversation_id = @ConversationId
                                 """;
        var remaining = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            countSql,
            new { ConversationId = participant.ConversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        return remaining;
    }

    private static ConversationParticipant MapToParticipant(ConversationParticipantRow row)
        => ConversationParticipant.Rehydrate(
            ConversationId.From(row.ConversationId),
            UserId.From(row.UserId),
            row.JoinedAtUtc,
            row.HiddenAtUtc);

    private sealed class ConversationParticipantRow
    {
        public Guid ConversationId { get; init; }

        public Guid UserId { get; init; }

        public DateTime JoinedAtUtc { get; init; }

        public DateTime? HiddenAtUtc { get; init; }
    }
}
