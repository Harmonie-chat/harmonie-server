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

    private static DirectMessage MapToDirectMessage(DirectMessageRow row)
    {
        var contentResult = ChannelMessageContent.Create(row.Content);
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
}
