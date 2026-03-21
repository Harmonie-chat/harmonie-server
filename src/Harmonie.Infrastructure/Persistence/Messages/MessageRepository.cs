using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence.Messages;

internal sealed class MessageRepository : IMessageRepository
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

        var attachmentsByMessageId = await MessageRepositoryHelpers.GetAttachmentsByMessageIdsAsync(
            _dbSession, [row.Id], cancellationToken);
        return MessageRepositoryHelpers.MapToMessage(row, attachmentsByMessageId);
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
}
