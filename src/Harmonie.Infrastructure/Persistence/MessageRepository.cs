using System.Text;
using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed class MessageRepository : IMessageRepository
{
    private readonly DbSession _dbSession;

    public MessageRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task AddAsync(
        Message message,
        CancellationToken cancellationToken = default)
    {
        var channelId = message.ChannelId;
        var conversationId = message.ConversationId;

        if ((channelId is null) == (conversationId is null))
            throw new InvalidOperationException("Message must have exactly one parent before persistence.");

        const string sql = """
                           INSERT INTO messages (
                               id,
                               channel_id,
                               conversation_id,
                               author_user_id,
                               content,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @ChannelId,
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
                ChannelId = channelId?.Value,
                ConversationId = conversationId?.Value,
                AuthorUserId = message.AuthorUserId.Value,
                Content = message.Content.Value,
                message.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<MessagePage> GetChannelPageAsync(
        GuildChannelId channelId,
        MessageCursor? beforeCursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;
        string sql;
        object parameters;

        if (beforeCursor is null)
        {
            sql = """
                  SELECT id AS "Id",
                         channel_id AS "ChannelId",
                         conversation_id AS "ConversationId",
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc",
                         deleted_at_utc AS "DeletedAtUtc"
                  FROM messages
                  WHERE channel_id = @ChannelId
                    AND deleted_at_utc IS NULL
                  ORDER BY created_at_utc DESC, id DESC
                  LIMIT @Take
                  """;

            parameters = new
            {
                ChannelId = channelId.Value,
                Take = take
            };
        }
        else
        {
            sql = """
                  SELECT id AS "Id",
                         channel_id AS "ChannelId",
                         conversation_id AS "ConversationId",
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc",
                         deleted_at_utc AS "DeletedAtUtc"
                  FROM messages
                  WHERE channel_id = @ChannelId
                    AND deleted_at_utc IS NULL
                    AND (created_at_utc, id) < (@BeforeCreatedAtUtc, @BeforeMessageId)
                  ORDER BY created_at_utc DESC, id DESC
                  LIMIT @Take
                  """;

            parameters = new
            {
                ChannelId = channelId.Value,
                BeforeCreatedAtUtc = beforeCursor.CreatedAtUtc,
                BeforeMessageId = beforeCursor.MessageId.Value,
                Take = take
            };
        }

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = (await connection.QueryAsync<MessageRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(MapToMessage)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        return new MessagePage(items, nextCursor);
    }

    public async Task<MessagePage> GetConversationPageAsync(
        ConversationId conversationId,
        MessageCursor? cursor,
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
                         channel_id AS "ChannelId",
                         conversation_id AS "ConversationId",
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc",
                         deleted_at_utc AS "DeletedAtUtc"
                  FROM messages
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
                         channel_id AS "ChannelId",
                         conversation_id AS "ConversationId",
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc",
                         deleted_at_utc AS "DeletedAtUtc"
                  FROM messages
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

        var rows = (await connection.QueryAsync<MessageRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(MapToMessage)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        return new MessagePage(items, nextCursor);
    }

    public async Task<SearchGuildMessagesPage> SearchGuildMessagesAsync(
        SearchGuildMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;

        var parameters = new DynamicParameters();
        parameters.Add("GuildId", query.GuildId.Value);
        parameters.Add("SearchText", query.SearchText);
        parameters.Add("Take", take);

        var sqlBuilder = new StringBuilder(
            """
            WITH search_query AS (
                SELECT websearch_to_tsquery('simple', @SearchText) AS ts_query
            )
            SELECT m.id AS "MessageId",
                   m.channel_id AS "ChannelId",
                   gc.name AS "ChannelName",
                   m.author_user_id AS "AuthorUserId",
                   u.username AS "AuthorUsername",
                   u.display_name AS "AuthorDisplayName",
                   m.content AS "Content",
                   m.created_at_utc AS "CreatedAtUtc",
                   m.updated_at_utc AS "UpdatedAtUtc"
            FROM messages m
            INNER JOIN guild_channels gc ON gc.id = m.channel_id
            INNER JOIN users u ON u.id = m.author_user_id
            CROSS JOIN search_query sq
            WHERE gc.guild_id = @GuildId
              AND m.deleted_at_utc IS NULL
              AND u.deleted_at IS NULL
              AND m.search_vector @@ sq.ts_query
            """);
        sqlBuilder.AppendLine();

        if (query.ChannelId is not null)
        {
            sqlBuilder.AppendLine("  AND m.channel_id = @ChannelId");
            parameters.Add("ChannelId", query.ChannelId.Value);
        }

        if (query.AuthorId is not null)
        {
            sqlBuilder.AppendLine("  AND m.author_user_id = @AuthorId");
            parameters.Add("AuthorId", query.AuthorId.Value);
        }

        if (query.BeforeCreatedAtUtc.HasValue)
        {
            sqlBuilder.AppendLine("  AND m.created_at_utc <= @BeforeCreatedAtUtc");
            parameters.Add("BeforeCreatedAtUtc", query.BeforeCreatedAtUtc.Value);
        }

        if (query.AfterCreatedAtUtc.HasValue)
        {
            sqlBuilder.AppendLine("  AND m.created_at_utc >= @AfterCreatedAtUtc");
            parameters.Add("AfterCreatedAtUtc", query.AfterCreatedAtUtc.Value);
        }

        if (query.Cursor is not null)
        {
            sqlBuilder.AppendLine("  AND (m.created_at_utc, m.id) < (@CursorCreatedAtUtc, @CursorMessageId)");
            parameters.Add("CursorCreatedAtUtc", query.Cursor.CreatedAtUtc);
            parameters.Add("CursorMessageId", query.Cursor.MessageId.Value);
        }

        sqlBuilder.AppendLine("ORDER BY m.created_at_utc DESC, m.id DESC");
        sqlBuilder.AppendLine("LIMIT @Take");
        var sql = sqlBuilder.ToString();

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = (await connection.QueryAsync<ChannelMessageSearchRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(MapToSearchGuildMessagesItem)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.MessageId.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.MessageId);
        }

        return new SearchGuildMessagesPage(items, nextCursor);
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

        var parameters = new DynamicParameters();
        parameters.Add("ConversationId", query.ConversationId.Value);
        parameters.Add("SearchText", query.SearchText);
        parameters.Add("Take", take);

        var sqlBuilder = new StringBuilder(
            """
            WITH search_query AS (
                SELECT websearch_to_tsquery('simple', @SearchText) AS ts_query
            )
            SELECT m.id AS "MessageId",
                   m.author_user_id AS "AuthorUserId",
                   u.username AS "AuthorUsername",
                   u.display_name AS "AuthorDisplayName",
                   u.avatar_url AS "AuthorAvatarUrl",
                   m.content AS "Content",
                   m.created_at_utc AS "CreatedAtUtc",
                   m.updated_at_utc AS "UpdatedAtUtc"
            FROM messages m
            INNER JOIN users u ON u.id = m.author_user_id
            CROSS JOIN search_query sq
            WHERE m.conversation_id = @ConversationId
              AND m.deleted_at_utc IS NULL
              AND m.search_vector @@ sq.ts_query
            """);
        sqlBuilder.AppendLine();

        if (query.BeforeCreatedAtUtc.HasValue)
        {
            sqlBuilder.AppendLine("  AND m.created_at_utc <= @BeforeCreatedAtUtc");
            parameters.Add("BeforeCreatedAtUtc", query.BeforeCreatedAtUtc.Value);
        }

        if (query.AfterCreatedAtUtc.HasValue)
        {
            sqlBuilder.AppendLine("  AND m.created_at_utc >= @AfterCreatedAtUtc");
            parameters.Add("AfterCreatedAtUtc", query.AfterCreatedAtUtc.Value);
        }

        if (query.Cursor is not null)
        {
            sqlBuilder.AppendLine("  AND (m.created_at_utc, m.id) < (@CursorCreatedAtUtc, @CursorMessageId)");
            parameters.Add("CursorCreatedAtUtc", query.Cursor.CreatedAtUtc);
            parameters.Add("CursorMessageId", query.Cursor.MessageId.Value);
        }

        sqlBuilder.AppendLine("ORDER BY m.created_at_utc DESC, m.id DESC");
        sqlBuilder.AppendLine("LIMIT @Take");
        var sql = sqlBuilder.ToString();

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

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.MessageId);
        }

        return new SearchConversationMessagesPage(items, nextCursor);
    }

    public async Task<Message?> GetByIdAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id AS "Id",
                                  channel_id AS "ChannelId",
                                  conversation_id AS "ConversationId",
                                  author_user_id AS "AuthorUserId",
                                  content AS "Content",
                                  created_at_utc AS "CreatedAtUtc",
                                  updated_at_utc AS "UpdatedAtUtc",
                                  deleted_at_utc AS "DeletedAtUtc"
                           FROM messages
                           WHERE id = @MessageId
                             AND deleted_at_utc IS NULL
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MessageRow>(command);
        return row is null ? null : MapToMessage(row);
    }

    public async Task UpdateAsync(
        Message message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE messages
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
        Message message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE messages
                           SET deleted_at_utc = @DeletedAtUtc,
                               updated_at_utc = @UpdatedAtUtc
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                message.DeletedAtUtc,
                message.UpdatedAtUtc,
                Id = message.Id.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private static Message MapToMessage(MessageRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored message content is invalid.");

        GuildChannelId? channelId = row.ChannelId.HasValue
            ? GuildChannelId.From(row.ChannelId.Value)
            : null;
        ConversationId? conversationId = row.ConversationId.HasValue
            ? ConversationId.From(row.ConversationId.Value)
            : null;

        return Message.Rehydrate(
            MessageId.From(row.Id),
            channelId,
            conversationId,
            UserId.From(row.AuthorUserId),
            contentResult.Value,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.DeletedAtUtc);
    }

    private static SearchGuildMessagesItem MapToSearchGuildMessagesItem(ChannelMessageSearchRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored guild message search content is invalid.");

        return new SearchGuildMessagesItem(
            MessageId: MessageId.From(row.MessageId),
            ChannelId: GuildChannelId.From(row.ChannelId),
            ChannelName: row.ChannelName,
            AuthorUserId: UserId.From(row.AuthorUserId),
            AuthorUsername: row.AuthorUsername,
            AuthorDisplayName: row.AuthorDisplayName,
            Content: contentResult.Value,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }

    private static SearchConversationMessagesItem MapToSearchConversationMessagesItem(DirectMessageSearchRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored conversation message search content is invalid.");

        return new SearchConversationMessagesItem(
            MessageId: MessageId.From(row.MessageId),
            AuthorUserId: UserId.From(row.AuthorUserId),
            AuthorUsername: row.AuthorUsername,
            AuthorDisplayName: row.AuthorDisplayName,
            AuthorAvatarUrl: row.AuthorAvatarUrl,
            Content: contentResult.Value,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }
}
