using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Dto;

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
                         created_at_utc AS "CreatedAtUtc"
                  FROM channel_messages
                  WHERE channel_id = @ChannelId
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
                         created_at_utc AS "CreatedAtUtc"
                  FROM channel_messages
                  WHERE channel_id = @ChannelId
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

        var rows = (await connection.QueryAsync<ChannelMessageDto>(command)).ToArray();
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

    private static ChannelMessage MapToChannelMessage(ChannelMessageDto row)
    {
        var contentResult = ChannelMessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored channel message content is invalid.");

        return ChannelMessage.Rehydrate(
            ChannelMessageId.From(row.Id),
            GuildChannelId.From(row.ChannelId),
            UserId.From(row.AuthorUserId),
            contentResult.Value,
            row.CreatedAtUtc);
    }
}
