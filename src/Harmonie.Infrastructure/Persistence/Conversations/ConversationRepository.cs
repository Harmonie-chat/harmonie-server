using Dapper;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Conversations;

namespace Harmonie.Infrastructure.Persistence.Conversations;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly DbSession _dbSession;

    public ConversationRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id             AS "Id",
                                  type           AS "Type",
                                  name           AS "Name",
                                  created_at_utc AS "CreatedAtUtc"
                           FROM conversations
                           WHERE id = @ConversationId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ConversationRow>(command);
        return row is null ? null : MapToConversation(row);
    }

    public async Task<ConversationGetOrCreateResult> GetOrCreateDirectAsync(
        UserId firstUserId,
        UserId secondUserId,
        CancellationToken cancellationToken = default)
    {
        if (firstUserId == secondUserId)
            throw new ArgumentException("Conversation participants must be different users.");

        var user1Id = firstUserId.Value.CompareTo(secondUserId.Value) <= 0
            ? firstUserId.Value
            : secondUserId.Value;
        var user2Id = user1Id == firstUserId.Value ? secondUserId.Value : firstUserId.Value;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        // Step 1: check if DM already exists
        const string selectLookupSql = """
                                        SELECT conversation_id AS "ConversationId"
                                        FROM direct_conversation_lookup
                                        WHERE user1_id = @User1Id AND user2_id = @User2Id
                                        """;
        var selectCommand = new CommandDefinition(
            selectLookupSql,
            new { User1Id = user1Id, User2Id = user2Id },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var existingConversationId = await connection.QueryFirstOrDefaultAsync<Guid?>(selectCommand);
        if (existingConversationId is not null)
        {
            // Clear hidden_at_utc for both participants so the conversation reappears
            const string clearHiddenSql = """
                                           UPDATE conversation_participants
                                           SET hidden_at_utc = NULL
                                           WHERE conversation_id = @ConversationId
                                             AND hidden_at_utc IS NOT NULL
                                           """;
            await connection.ExecuteAsync(new CommandDefinition(
                clearHiddenSql,
                new { ConversationId = existingConversationId.Value },
                transaction: _dbSession.Transaction,
                cancellationToken: cancellationToken));

            var existing = await GetByIdAsync(ConversationId.From(existingConversationId.Value), cancellationToken);
            return new ConversationGetOrCreateResult(existing!, WasCreated: false);
        }

        // Step 2: create the conversation, participants and lookup entry
        var newConversationId = ConversationId.New();
        var createdAtUtc = DateTime.UtcNow;

        const string insertConversationSql = """
                                              INSERT INTO conversations (id, type, name, created_at_utc)
                                              VALUES (@Id, 'direct', NULL, @CreatedAtUtc)
                                              """;
        await connection.ExecuteAsync(new CommandDefinition(
            insertConversationSql,
            new { Id = newConversationId.Value, CreatedAtUtc = createdAtUtc },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        const string insertParticipantsSql = """
                                              INSERT INTO conversation_participants (conversation_id, user_id, joined_at_utc)
                                              VALUES (@ConversationId, @UserId1, @JoinedAt),
                                                     (@ConversationId, @UserId2, @JoinedAt)
                                              """;
        await connection.ExecuteAsync(new CommandDefinition(
            insertParticipantsSql,
            new
            {
                ConversationId = newConversationId.Value,
                UserId1 = firstUserId.Value,
                UserId2 = secondUserId.Value,
                JoinedAt = createdAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        const string insertLookupSql = """
                                        INSERT INTO direct_conversation_lookup (user1_id, user2_id, conversation_id)
                                        VALUES (@User1Id, @User2Id, @ConversationId)
                                        ON CONFLICT (user1_id, user2_id) DO NOTHING
                                        """;
        await connection.ExecuteAsync(new CommandDefinition(
            insertLookupSql,
            new { User1Id = user1Id, User2Id = user2Id, ConversationId = newConversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        var conversation = Conversation.Rehydrate(newConversationId, ConversationType.Direct, null, createdAtUtc);
        return new ConversationGetOrCreateResult(conversation, WasCreated: true);
    }

    public async Task<Conversation> CreateGroupAsync(
        string? name,
        IReadOnlyList<UserId> participantIds,
        CancellationToken cancellationToken = default)
    {
        var conversationId = ConversationId.New();
        var createdAtUtc = DateTime.UtcNow;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string insertConversationSql = """
                                              INSERT INTO conversations (id, type, name, created_at_utc)
                                              VALUES (@Id, 'group', @Name, @CreatedAtUtc)
                                              """;
        var convCommand = new CommandDefinition(
            insertConversationSql,
            new { Id = conversationId.Value, Name = name, CreatedAtUtc = createdAtUtc },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(convCommand);

        const string insertParticipantSql = """
                                             INSERT INTO conversation_participants (conversation_id, user_id, joined_at_utc)
                                             VALUES (@ConversationId, @UserId, @JoinedAt)
                                             """;
        foreach (var participantId in participantIds)
        {
            var participantCommand = new CommandDefinition(
                insertParticipantSql,
                new { ConversationId = conversationId.Value, UserId = participantId.Value, JoinedAt = createdAtUtc },
                transaction: _dbSession.Transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(participantCommand);
        }

        return Conversation.Rehydrate(conversationId, ConversationType.Group, name, createdAtUtc);
    }

    public async Task<IReadOnlyList<UserConversationSummary>> GetUserConversationsAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT c.id              AS "ConversationId",
                                  c.type            AS "Type",
                                  c.name            AS "Name",
                                  c.created_at_utc  AS "CreatedAtUtc",
                                  cp2.user_id       AS "ParticipantUserId",
                                  u.username        AS "ParticipantUsername",
                                  u.display_name    AS "ParticipantDisplayName",
                                  u.avatar_file_id  AS "ParticipantAvatarFileId",
                                  u.avatar_color    AS "ParticipantAvatarColor",
                                  u.avatar_icon     AS "ParticipantAvatarIcon",
                                  u.avatar_bg       AS "ParticipantAvatarBg"
                           FROM conversation_participants cp1
                           INNER JOIN conversations c ON c.id = cp1.conversation_id
                           INNER JOIN conversation_participants cp2 ON cp2.conversation_id = c.id
                           INNER JOIN users u ON u.id = cp2.user_id
                           WHERE cp1.user_id = @UserId
                             AND cp1.hidden_at_utc IS NULL
                             AND u.deleted_at IS NULL
                           ORDER BY c.created_at_utc DESC, c.id ASC, cp2.user_id ASC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<UserConversationSummaryRow>(command);

        return rows
            .GroupBy(r => r.ConversationId)
            .Select(g =>
            {
                var first = g.First();
                var type = ParseConversationType(first.Type);
                var participants = g.Select(r =>
                {
                    var usernameResult = Username.Create(r.ParticipantUsername);
                    if (usernameResult.IsFailure || usernameResult.Value is null)
                        throw new InvalidOperationException("Stored conversation participant username is invalid.");
                    return new ConversationParticipantSummary(
                        UserId.From(r.ParticipantUserId),
                        usernameResult.Value,
                        r.ParticipantDisplayName,
                        r.ParticipantAvatarFileId,
                        r.ParticipantAvatarColor,
                        r.ParticipantAvatarIcon,
                        r.ParticipantAvatarBg);
                }).ToArray();

                return new UserConversationSummary(
                    ConversationId.From(first.ConversationId),
                    type,
                    first.Name,
                    participants,
                    first.CreatedAtUtc);
            })
            .ToArray();
    }

    public async Task<ConversationAccess?> GetByIdWithParticipantCheckAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT c.id             AS "Id",
                                  c.type           AS "Type",
                                  c.name           AS "Name",
                                  c.created_at_utc AS "CreatedAtUtc",
                                  EXISTS(
                                      SELECT 1 FROM conversation_participants
                                      WHERE conversation_id = c.id AND user_id = @UserId
                                  ) AS "IsParticipant"
                           FROM conversations c
                           WHERE c.id = @ConversationId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ConversationId = conversationId.Value, UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ConversationWithParticipantRow>(command);
        return row is null ? null : new ConversationAccess(MapToConversation(row), row.IsParticipant);
    }

    public async Task<int> RemoveParticipantAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string deleteSql = """
                                  DELETE FROM conversation_participants
                                  WHERE conversation_id = @ConversationId AND user_id = @UserId
                                  """;
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { ConversationId = conversationId.Value, UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        const string countSql = """
                                 SELECT COUNT(*) FROM conversation_participants
                                 WHERE conversation_id = @ConversationId
                                 """;
        var remaining = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            countSql,
            new { ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        return remaining;
    }

    public async Task HideConversationAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string sql = """
                            UPDATE conversation_participants
                            SET hidden_at_utc = NOW()
                            WHERE conversation_id = @ConversationId AND user_id = @UserId
                              AND hidden_at_utc IS NULL
                            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { ConversationId = conversationId.Value, UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        // direct_conversation_lookup does not have ON DELETE CASCADE on conversation_id, so delete it first
        const string deleteLookupSql = """
                                        DELETE FROM direct_conversation_lookup
                                        WHERE conversation_id = @ConversationId
                                        """;
        await connection.ExecuteAsync(new CommandDefinition(
            deleteLookupSql,
            new { ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));

        const string deleteConversationSql = """
                                              DELETE FROM conversations WHERE id = @ConversationId
                                              """;
        await connection.ExecuteAsync(new CommandDefinition(
            deleteConversationSql,
            new { ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        const string sql = """
                            UPDATE conversations
                            SET name = @Name
                            WHERE id = @Id
                            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Name = conversation.Name, Id = conversation.Id.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken));
    }

    private static Conversation MapToConversation(ConversationRow row)
        => Conversation.Rehydrate(
            ConversationId.From(row.Id),
            ParseConversationType(row.Type),
            row.Name,
            row.CreatedAtUtc);

    private static Conversation MapToConversation(ConversationWithParticipantRow row)
        => Conversation.Rehydrate(
            ConversationId.From(row.Id),
            ParseConversationType(row.Type),
            row.Name,
            row.CreatedAtUtc);

    private static ConversationType ParseConversationType(string type)
        => type.ToLowerInvariant() switch
        {
            "direct" => ConversationType.Direct,
            "group"  => ConversationType.Group,
            _        => throw new InvalidOperationException($"Unknown conversation type: '{type}'")
        };

    private sealed class ConversationWithParticipantRow
    {
        public Guid Id { get; init; }

        public string Type { get; init; } = string.Empty;

        public string? Name { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public bool IsParticipant { get; init; }
    }

}
