using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed class ChannelMessageRepository : IChannelMessageRepository
{
    private readonly DbSession _dbSession;

    public ChannelMessageRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task AddAsync(
        ChannelMessage message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO channel_messages (
                               id,
                               channel_id,
                               author_user_id,
                               content,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @ChannelId,
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
                ChannelId = message.ChannelId.Value,
                AuthorUserId = message.AuthorUserId.Value,
                Content = message.Content.Value,
                message.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<ChannelMessagePage> GetPageAsync(
        GuildChannelId channelId,
        ChannelMessageCursor? beforeCursor,
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
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc"
                  FROM channel_messages
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
                         author_user_id AS "AuthorUserId",
                         content AS "Content",
                         created_at_utc AS "CreatedAtUtc",
                         updated_at_utc AS "UpdatedAtUtc"
                  FROM channel_messages
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

        var rows = (await connection.QueryAsync<ChannelMessageRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(MapToChannelMessage)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        ChannelMessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new ChannelMessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        return new ChannelMessagePage(items, nextCursor);
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
        var filters = new List<string>
        {
            "gc.guild_id = @GuildId",
            "cm.deleted_at_utc IS NULL",
            "u.deleted_at IS NULL",
            "cm.search_vector @@ sq.ts_query"
        };

        var parameters = new DynamicParameters();
        parameters.Add("GuildId", query.GuildId.Value);
        parameters.Add("SearchText", query.SearchText);
        parameters.Add("Take", take);

        if (query.ChannelId is not null)
        {
            filters.Add("cm.channel_id = @ChannelId");
            parameters.Add("ChannelId", query.ChannelId.Value);
        }

        if (query.AuthorId is not null)
        {
            filters.Add("cm.author_user_id = @AuthorId");
            parameters.Add("AuthorId", query.AuthorId.Value);
        }

        if (query.BeforeCreatedAtUtc.HasValue)
        {
            filters.Add("cm.created_at_utc <= @BeforeCreatedAtUtc");
            parameters.Add("BeforeCreatedAtUtc", query.BeforeCreatedAtUtc.Value);
        }

        if (query.AfterCreatedAtUtc.HasValue)
        {
            filters.Add("cm.created_at_utc >= @AfterCreatedAtUtc");
            parameters.Add("AfterCreatedAtUtc", query.AfterCreatedAtUtc.Value);
        }

        if (query.Cursor is not null)
        {
            filters.Add("(cm.created_at_utc, cm.id) < (@CursorCreatedAtUtc, @CursorMessageId)");
            parameters.Add("CursorCreatedAtUtc", query.Cursor.CreatedAtUtc);
            parameters.Add("CursorMessageId", query.Cursor.MessageId.Value);
        }

        var whereClause = string.Join(Environment.NewLine + "  AND ", filters);
        var sql = $"""
                   WITH search_query AS (
                       SELECT websearch_to_tsquery('simple', @SearchText) AS ts_query
                   )
                   SELECT cm.id AS "MessageId",
                          cm.channel_id AS "ChannelId",
                          gc.name AS "ChannelName",
                          cm.author_user_id AS "AuthorUserId",
                          u.username AS "AuthorUsername",
                          u.display_name AS "AuthorDisplayName",
                          cm.content AS "Content",
                          cm.created_at_utc AS "CreatedAtUtc",
                          cm.updated_at_utc AS "UpdatedAtUtc"
                   FROM channel_messages cm
                   INNER JOIN guild_channels gc ON gc.id = cm.channel_id
                   INNER JOIN users u ON u.id = cm.author_user_id
                   CROSS JOIN search_query sq
                   WHERE {whereClause}
                   ORDER BY cm.created_at_utc DESC, cm.id DESC
                   LIMIT @Take
                   """;

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

        ChannelMessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new ChannelMessageCursor(oldestItem.CreatedAtUtc, oldestItem.MessageId);
        }

        return new SearchGuildMessagesPage(items, nextCursor);
    }

    public async Task<ChannelMessage?> GetByIdAsync(
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id AS "Id",
                                  channel_id AS "ChannelId",
                                  author_user_id AS "AuthorUserId",
                                  content AS "Content",
                                  created_at_utc AS "CreatedAtUtc",
                                  updated_at_utc AS "UpdatedAtUtc"
                           FROM channel_messages
                           WHERE id = @MessageId
                             AND deleted_at_utc IS NULL
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ChannelMessageRow>(command);
        return row is null ? null : MapToChannelMessage(row);
    }

    public async Task UpdateAsync(
        ChannelMessage message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE channel_messages
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

    public async Task DeleteAsync(
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE channel_messages
                           SET deleted_at_utc = @DeletedAtUtc
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                DeletedAtUtc = DateTime.UtcNow,
                Id = messageId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private static ChannelMessage MapToChannelMessage(ChannelMessageRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored channel message content is invalid.");

        return ChannelMessage.Rehydrate(
            ChannelMessageId.From(row.Id),
            GuildChannelId.From(row.ChannelId),
            UserId.From(row.AuthorUserId),
            contentResult.Value,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }

    private static SearchGuildMessagesItem MapToSearchGuildMessagesItem(ChannelMessageSearchRow row)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored channel message search content is invalid.");

        return new SearchGuildMessagesItem(
            MessageId: ChannelMessageId.From(row.MessageId),
            ChannelId: GuildChannelId.From(row.ChannelId),
            ChannelName: row.ChannelName,
            AuthorUserId: UserId.From(row.AuthorUserId),
            AuthorUsername: row.AuthorUsername,
            AuthorDisplayName: row.AuthorDisplayName,
            Content: contentResult.Value,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }
}
