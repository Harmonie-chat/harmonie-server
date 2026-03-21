using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed partial class MessageRepository : IMessageRepository
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

    private sealed class MessageAttachmentRow
    {
        public Guid MessageId { get; init; }
        public int Position { get; init; }
        public Guid UploadedFileId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
    }
}
