using System.Text;
using Dapper;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Channels;
using Harmonie.Infrastructure.Rows.Conversations;

namespace Harmonie.Infrastructure.Persistence.Messages;

internal sealed class MessageSearchRepository : IMessageSearchRepository
{
    private readonly DbSession _dbSession;

    public MessageSearchRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
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
        var attachmentsByMessageId = await MessageRepositoryHelpers.GetAttachmentsByMessageIdsAsync(
            _dbSession,
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
        var attachmentsByMessageId = await MessageRepositoryHelpers.GetAttachmentsByMessageIdsAsync(
            _dbSession,
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

    private static SearchGuildMessagesItem MapToSearchGuildMessagesItem(
        ChannelMessageSearchRow row,
        IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> attachmentsByMessageId)
    {
        MessageContent? messageContent = null;
        if (row.Content is not null)
        {
            var contentResult = MessageContent.Create(row.Content);
            if (contentResult.IsFailure || contentResult.Value is null)
                throw new InvalidOperationException("Stored guild message search content is invalid.");
            messageContent = contentResult.Value;
        }
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
            Content: messageContent,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }

    private static SearchConversationMessagesItem MapToSearchConversationMessagesItem(
        ConversationMessageSearchRow row,
        IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> attachmentsByMessageId)
    {
        MessageContent? convMessageContent = null;
        if (row.Content is not null)
        {
            var contentResult = MessageContent.Create(row.Content);
            if (contentResult.IsFailure || contentResult.Value is null)
                throw new InvalidOperationException("Stored conversation message search content is invalid.");
            convMessageContent = contentResult.Value;
        }
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
            Content: convMessageContent,
            CreatedAtUtc: row.CreatedAtUtc,
            UpdatedAtUtc: row.UpdatedAtUtc);
    }
}
