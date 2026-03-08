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
}
