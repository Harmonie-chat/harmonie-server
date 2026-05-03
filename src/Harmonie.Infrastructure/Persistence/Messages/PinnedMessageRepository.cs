using Dapper;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Messages;

namespace Harmonie.Infrastructure.Persistence.Messages;

public sealed class PinnedMessageRepository : IPinnedMessageRepository
{
    private readonly DbSession _dbSession;

    public PinnedMessageRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task AddAsync(
        PinnedMessage pinnedMessage,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO pinned_messages (message_id, pinned_by_user_id, pinned_at_utc)
                           VALUES (@MessageId, @PinnedByUserId, @PinnedAtUtc)
                           ON CONFLICT (message_id) DO NOTHING
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = pinnedMessage.MessageId.Value,
                PinnedByUserId = pinnedMessage.PinnedByUserId.Value,
                PinnedAtUtc = pinnedMessage.PinnedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task RemoveAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           DELETE FROM pinned_messages
                           WHERE message_id = @MessageId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<PinnedMessagesPage> GetPinnedMessagesAsync(
        GuildChannelId channelId,
        UserId callerId,
        PinnedMessagesCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await GetPinnedMessagesAsync(
            ("channel_id = @ContextId", new { ContextId = channelId.Value }),
            callerId,
            cursor,
            limit,
            cancellationToken);
    }

    public async Task<PinnedMessagesPage> GetPinnedMessagesAsync(
        ConversationId conversationId,
        UserId callerId,
        PinnedMessagesCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await GetPinnedMessagesAsync(
            ("conversation_id = @ContextId", new { ContextId = conversationId.Value }),
            callerId,
            cursor,
            limit,
            cancellationToken);
    }

    private async Task<PinnedMessagesPage> GetPinnedMessagesAsync(
        (string Filter, object Parameters) context,
        UserId callerId,
        PinnedMessagesCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;

        var cursorCondition = cursor is not null
            ? "AND (pm.pinned_at_utc, pm.message_id) < (@CursorPinnedAtUtc, @CursorMessageId)"
            : "1=1";

        var parameters = new DynamicParameters(context.Parameters);
        parameters.Add("Take", take);
        if (cursor is not null)
        {
            parameters.Add("CursorPinnedAtUtc", cursor.PinnedAtUtc);
            parameters.Add("CursorMessageId", cursor.MessageId);
        }

        var sql = $@"
                   SELECT m.id AS ""Id"",
                          m.author_user_id AS ""AuthorUserId"",
                          u.username AS ""AuthorUsername"",
                          u.display_name AS ""AuthorDisplayName"",
                          m.content AS ""Content"",
                          m.created_at_utc AS ""CreatedAtUtc"",
                          m.updated_at_utc AS ""UpdatedAtUtc"",
                          m.deleted_at_utc AS ""DeletedAtUtc"",
                          pm.pinned_by_user_id AS ""PinnedByUserId"",
                          pm.pinned_at_utc AS ""PinnedAtUtc""
                   FROM pinned_messages pm
                   INNER JOIN messages m ON m.id = pm.message_id
                   INNER JOIN users u ON u.id = m.author_user_id
                   WHERE m.{context.Filter}
                     AND {cursorCondition}
                   ORDER BY pm.pinned_at_utc DESC, pm.message_id DESC
                   LIMIT @Take;
                   ";

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = (await connection.QueryAsync<PinnedMessageWithContentRow>(command)).ToArray();

        if (rows.Length == 0)
            return new PinnedMessagesPage(Array.Empty<PinnedMessageSummary>(), null);

        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;

        var items = pageRows
            .Select(row => new PinnedMessageSummary(
                MessageId: row.Id,
                AuthorUserId: row.AuthorUserId,
                AuthorUsername: row.AuthorUsername ?? string.Empty,
                AuthorDisplayName: row.AuthorDisplayName,
                Content: row.DeletedAtUtc is null ? row.Content : null,
                CreatedAtUtc: row.CreatedAtUtc,
                UpdatedAtUtc: row.UpdatedAtUtc,
                PinnedByUserId: row.PinnedByUserId,
                PinnedAtUtc: row.PinnedAtUtc))
            .ToArray();

        PinnedMessagesCursor? nextCursor = null;
        if (hasMore && pageRows.Length > 0)
        {
            var lastRow = pageRows[^1];
            nextCursor = new PinnedMessagesCursor(lastRow.PinnedAtUtc, lastRow.Id);
        }

        return new PinnedMessagesPage(items, nextCursor);
    }

}

