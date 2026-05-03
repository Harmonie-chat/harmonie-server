using Dapper;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Messages;

namespace Harmonie.Infrastructure.Persistence.Messages;

internal sealed class MessagePaginationRepository : IMessagePaginationRepository
{
    private readonly DbSession _dbSession;

    public MessagePaginationRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

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
                          reply_to_message_id AS "ReplyToMessageId",
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

                   SELECT sub.message_id AS "MessageId",
                          sub.emoji AS "Emoji",
                          sub.user_id AS "UserId",
                          u.username AS "Username",
                          u.display_name AS "DisplayName"
                   FROM (
                       SELECT mr.message_id, mr.emoji, mr.user_id, mr.created_at_utc,
                              ROW_NUMBER() OVER (PARTITION BY mr.message_id, mr.emoji ORDER BY mr.created_at_utc) AS rn
                       FROM message_reactions mr
                       WHERE mr.message_id IN (
                           SELECT id FROM messages
                           WHERE channel_id = @ChannelId
                             AND deleted_at_utc IS NULL
                             {cursorFilter}
                           ORDER BY created_at_utc DESC, id DESC
                           LIMIT @Take)
                   ) sub
                   JOIN users u ON u.id = sub.user_id
                   WHERE sub.rn <= 5
                   ORDER BY sub.message_id, sub.emoji, sub.created_at_utc;

                   SELECT last_read_message_id AS "LastReadMessageId",
                          read_at_utc          AS "ReadAtUtc"
                   FROM channel_read_states
                   WHERE user_id    = @CallerId
                     AND channel_id = @ChannelId;

                   SELECT message_id AS "MessageId",
                          url AS "Url",
                          title AS "Title",
                          description AS "Description",
                          image_url AS "ImageUrl",
                          site_name AS "SiteName"
                   FROM message_link_previews
                   WHERE message_id IN (
                       SELECT id FROM messages
                       WHERE channel_id = @ChannelId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take)
                   ORDER BY message_id;

                   SELECT pm.message_id AS "MessageId"
                   FROM pinned_messages pm
                   WHERE pm.message_id IN (
                       SELECT id FROM messages
                       WHERE channel_id = @ChannelId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take);

                   SELECT m.id AS "TargetMessageId",
                          m.author_user_id AS "AuthorUserId",
                          u.username AS "AuthorUsername",
                          u.display_name AS "AuthorDisplayName",
                          LEFT(COALESCE(m.content, ''), 200) AS "Content",
                          m.deleted_at_utc IS NOT NULL AS "IsDeleted",
                          m.deleted_at_utc AS "DeletedAtUtc",
                          EXISTS(SELECT 1 FROM message_attachments ma2 WHERE ma2.message_id = m.id) AS "HasAttachments"
                   FROM messages m
                   JOIN users u ON u.id = m.author_user_id
                   WHERE m.id IN (
                       SELECT t.reply_to_message_id
                       FROM messages t
                       WHERE t.channel_id = @ChannelId
                         AND t.reply_to_message_id IS NOT NULL
                         AND t.deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY t.created_at_utc DESC, t.id DESC
                       LIMIT @Take)
                   ORDER BY m.id;
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
        var reactionUserRows = (await multi.ReadAsync<ReactionUserRow>()).ToArray();
        var readStateRow = await multi.ReadFirstOrDefaultAsync<ReadStateRow?>();
        var linkPreviewRows = (await multi.ReadAsync<MessageLinkPreviewRow>()).ToArray();
        var pinnedRows = (await multi.ReadAsync<PinnedMessageIdRow>()).ToArray();
        var replyPreviewRows = (await multi.ReadAsync<ReplyPreviewRow>()).ToArray();

        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;
        var pageMessageIds = new HashSet<Guid>(pageRows.Select(row => row.Id));

        var attachmentsByMessageId = MessageRepositoryHelpers.BuildAttachmentsDictionary(
            attachmentRows.Where(r => pageMessageIds.Contains(r.MessageId)));
        var reactionsByMessageId = MessageRepositoryHelpers.BuildReactionsDictionary(
            reactionRows.Where(r => pageMessageIds.Contains(r.MessageId)),
            reactionUserRows.Where(r => pageMessageIds.Contains(r.MessageId)));
        var linkPreviewsByMessageId = MessageRepositoryHelpers.BuildLinkPreviewsDictionary(
            linkPreviewRows.Where(r => pageMessageIds.Contains(r.MessageId)));

        var pinnedMessageIds = new HashSet<Guid>(
            pinnedRows.Where(r => pageMessageIds.Contains(r.MessageId)).Select(r => r.MessageId));

        var replyPreviewsByTargetMessageId = replyPreviewRows
            .ToDictionary(
                r => r.TargetMessageId,
                r => new ReplyPreviewDto(
                    r.TargetMessageId,
                    r.AuthorUserId,
                    r.AuthorDisplayName,
                    r.AuthorUsername,
                    r.IsDeleted ? null : r.Content,
                    r.HasAttachments,
                    r.IsDeleted,
                    r.DeletedAtUtc));

        var items = pageRows
            .Select(row => MessageRepositoryHelpers.MapToMessage(row, attachmentsByMessageId))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        MessageReadState? lastReadState = null;
        if (readStateRow is not null)
        {
            lastReadState = MessageReadState.Rehydrate(
                UserId.From(callerId.Value),
                GuildChannelId.From(channelId.Value),
                conversationId: null,
                MessageId.From(readStateRow.LastReadMessageId),
                readStateRow.ReadAtUtc);
        }

        return new MessagePage(items, nextCursor, reactionsByMessageId, linkPreviewsByMessageId, pinnedMessageIds, replyPreviewsByTargetMessageId, lastReadState);
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
                          reply_to_message_id AS "ReplyToMessageId",
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

                   SELECT sub.message_id AS "MessageId",
                          sub.emoji AS "Emoji",
                          sub.user_id AS "UserId",
                          u.username AS "Username",
                          u.display_name AS "DisplayName"
                   FROM (
                       SELECT mr.message_id, mr.emoji, mr.user_id, mr.created_at_utc,
                              ROW_NUMBER() OVER (PARTITION BY mr.message_id, mr.emoji ORDER BY mr.created_at_utc) AS rn
                       FROM message_reactions mr
                       WHERE mr.message_id IN (
                           SELECT id FROM messages
                           WHERE conversation_id = @ConversationId
                             AND deleted_at_utc IS NULL
                             {cursorFilter}
                           ORDER BY created_at_utc DESC, id DESC
                           LIMIT @Take)
                   ) sub
                   JOIN users u ON u.id = sub.user_id
                   WHERE sub.rn <= 5
                   ORDER BY sub.message_id, sub.emoji, sub.created_at_utc;

                   SELECT last_read_message_id AS "LastReadMessageId",
                          read_at_utc          AS "ReadAtUtc"
                   FROM conversation_read_states
                   WHERE user_id         = @CallerId
                     AND conversation_id = @ConversationId;

                   SELECT message_id AS "MessageId",
                          url AS "Url",
                          title AS "Title",
                          description AS "Description",
                          image_url AS "ImageUrl",
                          site_name AS "SiteName"
                   FROM message_link_previews
                   WHERE message_id IN (
                       SELECT id FROM messages
                       WHERE conversation_id = @ConversationId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take)
                   ORDER BY message_id;

                   SELECT pm.message_id AS "MessageId"
                   FROM pinned_messages pm
                   WHERE pm.message_id IN (
                       SELECT id FROM messages
                       WHERE conversation_id = @ConversationId
                         AND deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY created_at_utc DESC, id DESC
                       LIMIT @Take);

                   SELECT m.id AS "TargetMessageId",
                          m.author_user_id AS "AuthorUserId",
                          u.username AS "AuthorUsername",
                          u.display_name AS "AuthorDisplayName",
                          LEFT(COALESCE(m.content, ''), 200) AS "Content",
                          m.deleted_at_utc IS NOT NULL AS "IsDeleted",
                          m.deleted_at_utc AS "DeletedAtUtc",
                          EXISTS(SELECT 1 FROM message_attachments ma2 WHERE ma2.message_id = m.id) AS "HasAttachments"
                   FROM messages m
                   JOIN users u ON u.id = m.author_user_id
                   WHERE m.id IN (
                       SELECT t.reply_to_message_id
                       FROM messages t
                       WHERE t.conversation_id = @ConversationId
                         AND t.reply_to_message_id IS NOT NULL
                         AND t.deleted_at_utc IS NULL
                         {cursorFilter}
                       ORDER BY t.created_at_utc DESC, t.id DESC
                       LIMIT @Take)
                   ORDER BY m.id;
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
        var reactionUserRows = (await multi.ReadAsync<ReactionUserRow>()).ToArray();
        var readStateRow = await multi.ReadFirstOrDefaultAsync<ReadStateRow?>();
        var linkPreviewRows = (await multi.ReadAsync<MessageLinkPreviewRow>()).ToArray();
        var pinnedRows = (await multi.ReadAsync<PinnedMessageIdRow>()).ToArray();
        var replyPreviewRows = (await multi.ReadAsync<ReplyPreviewRow>()).ToArray();

        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;
        var pageMessageIds = new HashSet<Guid>(pageRows.Select(row => row.Id));

        var attachmentsByMessageId = MessageRepositoryHelpers.BuildAttachmentsDictionary(
            attachmentRows.Where(r => pageMessageIds.Contains(r.MessageId)));
        var reactionsByMessageId = MessageRepositoryHelpers.BuildReactionsDictionary(
            reactionRows.Where(r => pageMessageIds.Contains(r.MessageId)),
            reactionUserRows.Where(r => pageMessageIds.Contains(r.MessageId)));
        var linkPreviewsByMessageId = MessageRepositoryHelpers.BuildLinkPreviewsDictionary(
            linkPreviewRows.Where(r => pageMessageIds.Contains(r.MessageId)));

        var pinnedMessageIds = new HashSet<Guid>(
            pinnedRows.Where(r => pageMessageIds.Contains(r.MessageId)).Select(r => r.MessageId));

        var replyPreviewsByTargetMessageId = replyPreviewRows
            .ToDictionary(
                r => r.TargetMessageId,
                r => new ReplyPreviewDto(
                    r.TargetMessageId,
                    r.AuthorUserId,
                    r.AuthorDisplayName,
                    r.AuthorUsername,
                    r.IsDeleted ? null : r.Content,
                    r.HasAttachments,
                    r.IsDeleted,
                    r.DeletedAtUtc));

        var items = pageRows
            .Select(row => MessageRepositoryHelpers.MapToMessage(row, attachmentsByMessageId))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .ToArray();

        MessageCursor? nextCursor = null;
        if (hasMore && items.Length > 0)
        {
            var oldestItem = items[0];
            nextCursor = new MessageCursor(oldestItem.CreatedAtUtc, oldestItem.Id);
        }

        MessageReadState? lastReadState = null;
        if (readStateRow is not null)
        {
            lastReadState = MessageReadState.Rehydrate(
                UserId.From(callerId.Value),
                channelId: null,
                ConversationId.From(conversationId.Value),
                MessageId.From(readStateRow.LastReadMessageId),
                readStateRow.ReadAtUtc);
        }

        return new MessagePage(items, nextCursor, reactionsByMessageId, linkPreviewsByMessageId, pinnedMessageIds, replyPreviewsByTargetMessageId, lastReadState);
    }

    private sealed class ReadStateRow
    {
        public Guid LastReadMessageId { get; init; }
        public DateTime ReadAtUtc { get; init; }
    }
}
