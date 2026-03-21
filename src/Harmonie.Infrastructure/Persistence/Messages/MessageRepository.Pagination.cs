using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed partial class MessageRepository
{
    public async Task<MessagePage> GetChannelPageAsync(
        GuildChannelId channelId,
        MessageCursor? beforeCursor,
        int limit,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;

        string cursorFilter = beforeCursor is not null
            ? "AND (created_at_utc, id) < (@BeforeCreatedAtUtc, @BeforeMessageId)"
            : "";

        var sql = $"""
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
                     {cursorFilter}
                   ORDER BY created_at_utc DESC, id DESC
                   LIMIT @Take;

                   SELECT ma.message_id AS "MessageId",
                          ma.position AS "Position",
                          uf.id AS "UploadedFileId",
                          uf.filename AS "FileName",
                          uf.content_type AS "ContentType",
                          uf.size_bytes AS "SizeBytes"
                   FROM message_attachments ma
                   INNER JOIN uploaded_files uf ON uf.id = ma.uploaded_file_id
                   WHERE ma.message_id IN (
                       SELECT id FROM messages
                       WHERE channel_id = @ChannelId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take)
                   ORDER BY ma.message_id, ma.position;

                   SELECT message_id AS "MessageId",
                          emoji AS "Emoji",
                          COUNT(*) AS "Count",
                          BOOL_OR(user_id = @CallerId) AS "ReactedByCaller"
                   FROM message_reactions
                   WHERE message_id IN (
                       SELECT id FROM messages
                       WHERE channel_id = @ChannelId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take)
                   GROUP BY message_id, emoji
                   ORDER BY message_id, MIN(created_at_utc);

                   SELECT last_read_message_id
                   FROM channel_read_states
                   WHERE user_id    = @CallerId
                     AND channel_id = @ChannelId;
                   """;

        var parameters = new DynamicParameters();
        parameters.Add("ChannelId", channelId.Value);
        parameters.Add("Take", take);
        parameters.Add("CallerId", callerId.Value);
        if (beforeCursor is not null)
        {
            parameters.Add("BeforeCreatedAtUtc", beforeCursor.CreatedAtUtc);
            parameters.Add("BeforeMessageId", beforeCursor.MessageId.Value);
        }

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<MessageRow>()).ToArray();
        var attachmentRows = (await multi.ReadAsync<MessageAttachmentRow>()).ToArray();
        var reactionRows = (await multi.ReadAsync<ReactionSummaryRow>()).ToArray();
        var lastReadMessageIdRaw = await multi.ReadSingleOrDefaultAsync<Guid?>();

        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;
        var pageMessageIds = new HashSet<Guid>(pageRows.Select(row => row.Id));

        var attachmentsByMessageId = BuildAttachmentsDictionary(
            attachmentRows.Where(r => pageMessageIds.Contains(r.MessageId)));
        var reactionsByMessageId = BuildReactionsDictionary(
            reactionRows.Where(r => pageMessageIds.Contains(r.MessageId)));

        var items = pageRows
            .Select(row => MapToMessage(row, attachmentsByMessageId))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        var lastReadMessageId = lastReadMessageIdRaw.HasValue
            ? MessageId.From(lastReadMessageIdRaw.Value)
            : null;

        return new MessagePage(items, nextCursor, reactionsByMessageId, lastReadMessageId);
    }

    public async Task<MessagePage> GetConversationPageAsync(
        ConversationId conversationId,
        MessageCursor? cursor,
        int limit,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var take = limit + 1;

        string cursorFilter = cursor is not null
            ? "AND (created_at_utc, id) < (@BeforeCreatedAtUtc, @BeforeMessageId)"
            : "";

        var sql = $"""
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
                     {cursorFilter}
                   ORDER BY created_at_utc DESC, id DESC
                   LIMIT @Take;

                   SELECT ma.message_id AS "MessageId",
                          ma.position AS "Position",
                          uf.id AS "UploadedFileId",
                          uf.filename AS "FileName",
                          uf.content_type AS "ContentType",
                          uf.size_bytes AS "SizeBytes"
                   FROM message_attachments ma
                   INNER JOIN uploaded_files uf ON uf.id = ma.uploaded_file_id
                   WHERE ma.message_id IN (
                       SELECT id FROM messages
                       WHERE conversation_id = @ConversationId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take)
                   ORDER BY ma.message_id, ma.position;

                   SELECT message_id AS "MessageId",
                          emoji AS "Emoji",
                          COUNT(*) AS "Count",
                          BOOL_OR(user_id = @CallerId) AS "ReactedByCaller"
                   FROM message_reactions
                   WHERE message_id IN (
                       SELECT id FROM messages
                       WHERE conversation_id = @ConversationId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take)
                   GROUP BY message_id, emoji
                   ORDER BY message_id, MIN(created_at_utc);

                   SELECT last_read_message_id
                   FROM conversation_read_states
                   WHERE user_id         = @CallerId
                     AND conversation_id = @ConversationId;
                   """;

        var parameters = new DynamicParameters();
        parameters.Add("ConversationId", conversationId.Value);
        parameters.Add("Take", take);
        parameters.Add("CallerId", callerId.Value);
        if (cursor is not null)
        {
            parameters.Add("BeforeCreatedAtUtc", cursor.CreatedAtUtc);
            parameters.Add("BeforeMessageId", cursor.MessageId.Value);
        }

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<MessageRow>()).ToArray();
        var attachmentRows = (await multi.ReadAsync<MessageAttachmentRow>()).ToArray();
        var reactionRows = (await multi.ReadAsync<ReactionSummaryRow>()).ToArray();
        var lastReadMessageIdRaw = await multi.ReadSingleOrDefaultAsync<Guid?>();

        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;
        var pageMessageIds = new HashSet<Guid>(pageRows.Select(row => row.Id));

        var attachmentsByMessageId = BuildAttachmentsDictionary(
            attachmentRows.Where(r => pageMessageIds.Contains(r.MessageId)));
        var reactionsByMessageId = BuildReactionsDictionary(
            reactionRows.Where(r => pageMessageIds.Contains(r.MessageId)));

        var items = pageRows
            .Select(row => MapToMessage(row, attachmentsByMessageId))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        var lastReadMessageId = lastReadMessageIdRaw.HasValue
            ? MessageId.From(lastReadMessageIdRaw.Value)
            : null;

        return new MessagePage(items, nextCursor, reactionsByMessageId, lastReadMessageId);
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<MessageReactionSummary>> BuildReactionsDictionary(
        IEnumerable<ReactionSummaryRow> rows)
    {
        return rows
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MessageReactionSummary>)group
                    .Select(row => new MessageReactionSummary(
                        row.Emoji,
                        row.Count,
                        row.ReactedByCaller))
                    .ToArray());
    }

    private sealed class ReactionSummaryRow
    {
        public Guid MessageId { get; init; }
        public string Emoji { get; init; } = string.Empty;
        public int Count { get; init; }
        public bool ReactedByCaller { get; init; }
    }
}
