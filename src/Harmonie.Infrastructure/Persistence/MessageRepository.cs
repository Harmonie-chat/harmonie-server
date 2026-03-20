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

        if (message.Attachments.Count == 0)
            return;

        const string attachmentSql = """
                                     INSERT INTO message_attachments (
                                         message_id,
                                         uploaded_file_id,
                                         position)
                                     VALUES (
                                         @MessageId,
                                         @UploadedFileId,
                                         @Position)
                                     """;

        var attachmentCommand = new CommandDefinition(
            attachmentSql,
            message.Attachments.Select((attachment, index) => new
            {
                MessageId = message.Id.Value,
                UploadedFileId = attachment.FileId.Value,
                Position = index
            }),
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(attachmentCommand);
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
                   u.avatar_file_id AS "AuthorAvatarFileId",
                   u.avatar_color AS "AuthorAvatarColor",
                   u.avatar_icon AS "AuthorAvatarIcon",
                   u.avatar_bg AS "AuthorAvatarBg",
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
        var attachmentsByMessageId = await GetAttachmentsByMessageIdsAsync(
            pageRows.Select(row => row.MessageId).ToArray(),
            cancellationToken);

        var items = pageRows
            .Select(row => MapToSearchGuildMessagesItem(row, attachmentsByMessageId))
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
                   u.avatar_file_id AS "AuthorAvatarFileId",
                   u.avatar_color AS "AuthorAvatarColor",
                   u.avatar_icon AS "AuthorAvatarIcon",
                   u.avatar_bg AS "AuthorAvatarBg",
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

        var rows = (await connection.QueryAsync<ConversationMessageSearchRow>(command)).ToArray();
        var hasMore = rows.Length > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows;
        var attachmentsByMessageId = await GetAttachmentsByMessageIdsAsync(
            pageRows.Select(row => row.MessageId).ToArray(),
            cancellationToken);

        var items = pageRows
            .Select(row => MapToSearchConversationMessagesItem(row, attachmentsByMessageId))
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
        if (row is null)
            return null;

        var attachmentsByMessageId = await GetAttachmentsByMessageIdsAsync([row.Id], cancellationToken);
        return MapToMessage(row, attachmentsByMessageId);
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

    public async Task<MessageId?> GetLatestChannelMessageIdAsync(
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id
                           FROM messages
                           WHERE channel_id = @ChannelId
                             AND deleted_at_utc IS NULL
                           ORDER BY created_at_utc DESC, id DESC
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ChannelId = channelId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QuerySingleOrDefaultAsync<Guid?>(command);
        return result.HasValue ? MessageId.From(result.Value) : null;
    }

    public async Task<MessageId?> GetLatestConversationMessageIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id
                           FROM messages
                           WHERE conversation_id = @ConversationId
                             AND deleted_at_utc IS NULL
                           ORDER BY created_at_utc DESC, id DESC
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ConversationId = conversationId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QuerySingleOrDefaultAsync<Guid?>(command);
        return result.HasValue ? MessageId.From(result.Value) : null;
    }

    public async Task<int> SoftDeleteByAuthorInGuildAsync(
        GuildId guildId,
        UserId authorUserId,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0)
            return 0;

        const string sql = """
                           UPDATE messages m
                           SET deleted_at_utc = NOW(),
                               updated_at_utc = NOW()
                           FROM guild_channels gc
                           WHERE m.channel_id = gc.id
                             AND gc.guild_id = @GuildId
                             AND m.author_user_id = @AuthorUserId
                             AND m.deleted_at_utc IS NULL
                             AND m.created_at_utc >= NOW() - @Interval::interval
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = guildId.Value,
                AuthorUserId = authorUserId.Value,
                Interval = $"{days} days"
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteAsync(command);
    }

    public async Task RemoveAttachmentAsync(
        MessageId messageId,
        UploadedFileId attachmentFileId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           DELETE FROM message_attachments
                           WHERE message_id = @MessageId
                             AND uploaded_file_id = @UploadedFileId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = messageId.Value,
                UploadedFileId = attachmentFileId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>>> GetAttachmentsByMessageIdsAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<MessageAttachment>>();

        const string sql = """
                           SELECT ma.message_id AS "MessageId",
                                  ma.position AS "Position",
                                  uf.id AS "UploadedFileId",
                                  uf.filename AS "FileName",
                                  uf.content_type AS "ContentType",
                                  uf.size_bytes AS "SizeBytes"
                           FROM message_attachments ma
                           INNER JOIN uploaded_files uf ON uf.id = ma.uploaded_file_id
                           WHERE ma.message_id = ANY(@MessageIds)
                           ORDER BY ma.message_id, ma.position
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageIds = messageIds.ToArray() },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<MessageAttachmentRow>(command);

        return rows
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MessageAttachment>)group
                    .OrderBy(row => row.Position)
                    .Select(row => new MessageAttachment(
                        UploadedFileId.From(row.UploadedFileId),
                        row.FileName,
                        row.ContentType,
                        row.SizeBytes))
                    .ToArray());
    }

    private static Message MapToMessage(
        MessageRow row,
        IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> attachmentsByMessageId)
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
        attachmentsByMessageId.TryGetValue(row.Id, out var attachments);

        return Message.Rehydrate(
            MessageId.From(row.Id),
            channelId,
            conversationId,
            UserId.From(row.AuthorUserId),
            contentResult.Value,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.DeletedAtUtc,
            attachments);
    }

    private static SearchGuildMessagesItem MapToSearchGuildMessagesItem(
        ChannelMessageSearchRow row,
        IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> attachmentsByMessageId)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored guild message search content is invalid.");
        attachmentsByMessageId.TryGetValue(row.MessageId, out var attachments);

        return new SearchGuildMessagesItem(
            MessageId: MessageId.From(row.MessageId),
            ChannelId: GuildChannelId.From(row.ChannelId),
            ChannelName: row.ChannelName,
            AuthorUserId: UserId.From(row.AuthorUserId),
            AuthorUsername: row.AuthorUsername,
            AuthorDisplayName: row.AuthorDisplayName,
            AuthorAvatarFileId: row.AuthorAvatarFileId.HasValue ? UploadedFileId.From(row.AuthorAvatarFileId.Value) : null,
            AuthorAvatarColor: row.AuthorAvatarColor,
            AuthorAvatarIcon: row.AuthorAvatarIcon,
            AuthorAvatarBg: row.AuthorAvatarBg,
            Attachments: attachments ?? Array.Empty<MessageAttachment>(),
            Content: contentResult.Value,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }

    private static SearchConversationMessagesItem MapToSearchConversationMessagesItem(
        ConversationMessageSearchRow row,
        IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> attachmentsByMessageId)
    {
        var contentResult = MessageContent.Create(row.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Stored conversation message search content is invalid.");
        attachmentsByMessageId.TryGetValue(row.MessageId, out var attachments);

        return new SearchConversationMessagesItem(
            MessageId: MessageId.From(row.MessageId),
            AuthorUserId: UserId.From(row.AuthorUserId),
            AuthorUsername: row.AuthorUsername,
            AuthorDisplayName: row.AuthorDisplayName,
            AuthorAvatarFileId: row.AuthorAvatarFileId.HasValue ? UploadedFileId.From(row.AuthorAvatarFileId.Value) : null,
            AuthorAvatarColor: row.AuthorAvatarColor,
            AuthorAvatarIcon: row.AuthorAvatarIcon,
            AuthorAvatarBg: row.AuthorAvatarBg,
            Attachments: attachments ?? Array.Empty<MessageAttachment>(),
            Content: contentResult.Value,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> BuildAttachmentsDictionary(
        IEnumerable<MessageAttachmentRow> rows)
    {
        return rows
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MessageAttachment>)group
                    .OrderBy(row => row.Position)
                    .Select(row => new MessageAttachment(
                        UploadedFileId.From(row.UploadedFileId),
                        row.FileName,
                        row.ContentType,
                        row.SizeBytes))
                    .ToArray());
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

    private sealed class MessageAttachmentRow
    {
        public Guid MessageId { get; init; }
        public int Position { get; init; }
        public Guid UploadedFileId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
    }

    private sealed class ReactionSummaryRow
    {
        public Guid MessageId { get; init; }
        public string Emoji { get; init; } = string.Empty;
        public int Count { get; init; }
        public bool ReactedByCaller { get; init; }
    }
}
