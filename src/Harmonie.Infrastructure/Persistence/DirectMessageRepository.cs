using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed class DirectMessageRepository : IDirectMessageRepository
{
    private readonly DbSession _dbSession;

    public DirectMessageRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task AddAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO direct_messages (
                               id,
                               conversation_id,
                               author_user_id,
                               content,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @ConversationId,
                               @AuthorUserId,
                               @Content,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = message.Id.Value,
                ConversationId = message.ConversationId.Value,
                AuthorUserId = message.AuthorUserId.Value,
                Content = message.Content.Value,
                message.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<DirectMessagePage> GetMessagesAsync(
        ConversationId conversationId,
        DirectMessageCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;
        string sql;
        object parameters;

        if (cursor is null)
        {
            sql = """
                  SELECT id AS "Id",
                         conversation_id AS "ConversationId",
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc",
                         deleted_at_utc AS "DeletedAtUtc"
                  FROM direct_messages
                  WHERE conversation_id = @ConversationId
                    AND deleted_at_utc IS NULL
                  ORDER BY created_at_utc DESC, id DESC
                  LIMIT @Take
                  """;

            parameters = new
            {
                ConversationId = conversationId.Value,
                Take = take
            };
        }
        else
        {
            sql = """
                  SELECT id AS "Id",
                         conversation_id AS "ConversationId",
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc",
                         deleted_at_utc AS "DeletedAtUtc"
                  FROM direct_messages
                  WHERE conversation_id = @ConversationId
                    AND deleted_at_utc IS NULL
                    AND (created_at_utc, id) < (@BeforeCreatedAtUtc, @BeforeMessageId)
                  ORDER BY created_at_utc DESC, id DESC
                  LIMIT @Take
                  """;

            parameters = new
            {
                ConversationId = conversationId.Value,
                BeforeCreatedAtUtc = cursor.CreatedAtUtc,
                BeforeMessageId = cursor.MessageId.Value,
                Take = take
            };
        }

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = (await connection.QueryAsync<DirectMessageRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(MapToDirectMessage)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        DirectMessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new DirectMessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        return new DirectMessagePage(items, nextCursor);
    }

    public async Task<SearchConversationMessagesPage> SearchConversationMessagesAsync(
        SearchConversationMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;

        var filters = new List<string>
        {
            "dm.conversation_id = @ConversationId",
            "dm.deleted_at_utc IS NULL",
            "dm.search_vector @@ sq.ts_query"
        };

        var parameters = new DynamicParameters();
        parameters.Add("ConversationId", query.ConversationId.Value);
        parameters.Add("SearchText", query.SearchText);
        parameters.Add("Take", take);

        if (query.BeforeCreatedAtUtc.HasValue)
        {
            filters.Add("dm.created_at_utc <= @BeforeCreatedAtUtc");
            parameters.Add("BeforeCreatedAtUtc", query.BeforeCreatedAtUtc.Value);
        }

        if (query.AfterCreatedAtUtc.HasValue)
        {
            filters.Add("dm.created_at_utc >= @AfterCreatedAtUtc");
            parameters.Add("AfterCreatedAtUtc", query.AfterCreatedAtUtc.Value);
        }

        if (query.Cursor is not null)
        {
            filters.Add("(dm.created_at_utc, dm.id) < (@CursorCreatedAtUtc, @CursorMessageId)");
            parameters.Add("CursorCreatedAtUtc", query.Cursor.CreatedAtUtc);
            parameters.Add("CursorMessageId", query.Cursor.MessageId.Value);
        }

        var whereClause = string.Join(Environment.NewLine + "  AND ", filters);
        var sql = $"""
                   WITH search_query AS (
                       SELECT websearch_to_tsquery('simple', @SearchText) AS ts_query
                   )
                   SELECT dm.id AS "MessageId",
                          dm.author_user_id AS "AuthorUserId",
                          u.username AS "AuthorUsername",
                          u.display_name AS "AuthorDisplayName",
                          u.avatar_url AS "AuthorAvatarUrl",
                          dm.content AS "Content",
                          dm.created_at_utc AS "CreatedAtUtc",
                          dm.updated_at_utc AS "UpdatedAtUtc"
                   FROM direct_messages dm
                   INNER JOIN users u ON u.id = dm.author_user_id
                   CROSS JOIN search_query sq
                   WHERE {whereClause}
                   ORDER BY dm.created_at_utc DESC, dm.id DESC
                   LIMIT @Take
                   """;

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = (await connection.QueryAsync<DirectMessageSearchRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(MapToSearchConversationMessagesItem)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.MessageId.Value)
            .ToArray();

        DirectMessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new DirectMessageCursor(oldestItem.CreatedAtUtc, oldestItem.MessageId);
        }

        return new SearchConversationMessagesPage(items, nextCursor);
    }

    public async Task<DirectMessage?> GetByIdAsync(
        DirectMessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id AS "Id",
                                  conversation_id AS "ConversationId",
                                  author_user_id AS "AuthorUserId",
                                  content AS "Content",
                                  created_at_utc AS "CreatedAtUtc",
                                  updated_at_utc AS "UpdatedAtUtc",
                                  deleted_at_utc AS "DeletedAtUtc"
                           FROM direct_messages
                           WHERE id = @MessageId
                             AND deleted_at_utc IS NULL
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DirectMessageRow>(command);
        return row is null ? null : MapToDirectMessage(row);
    }

    public async Task UpdateContentAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE direct_messages
                           SET content = @Content,
                               updated_at_utc = @UpdatedAtUtc
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Content = message.Content.Value,
                message.UpdatedAtUtc,
                Id = message.Id.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task SoftDeleteAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE direct_messages
                           SET updated_at_utc = @UpdatedAtUtc,
                               deleted_at_utc = @DeletedAtUtc
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                message.UpdatedAtUtc,
                message.DeletedAtUtc,
                Id = message.Id.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private static DirectMessage MapToDirectMessage(DirectMessageRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored direct message content is invalid.");

        return DirectMessage.Rehydrate(
            DirectMessageId.From(row.Id),
            ConversationId.From(row.ConversationId),
            UserId.From(row.AuthorUserId),
            contentResult.Value,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.DeletedAtUtc);
    }

    private static SearchConversationMessagesItem MapToSearchConversationMessagesItem(DirectMessageSearchRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored direct message search content is invalid.");

        return new SearchConversationMessagesItem(
            MessageId: DirectMessageId.From(row.MessageId),
            AuthorUserId: UserId.From(row.AuthorUserId),
            AuthorUsername: row.AuthorUsername,
            AuthorDisplayName: row.AuthorDisplayName,
            AuthorAvatarUrl: row.AuthorAvatarUrl,
            Content: contentResult.Value,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }
}
