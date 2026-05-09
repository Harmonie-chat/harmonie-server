using Dapper;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Messages;

namespace Harmonie.Infrastructure.Persistence.Messages;

internal sealed class ReplyTargetSummaryRow
{
    public Guid MessageId { get; init; }
    public Guid? ChannelId { get; init; }
    public Guid? ConversationId { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorUsername { get; init; } = string.Empty;
    public string? AuthorDisplayName { get; init; }
    public string? Content { get; init; }
    public bool HasAttachments { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
}

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
        var (channelId, conversationId) = MessageRepositoryHelpers.SplitScope(message.Scope);

        const string sql = """
                           INSERT INTO messages (
                               id,
                               channel_id,
                               conversation_id,
                               author_user_id,
                               reply_to_message_id,
                               content,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @ChannelId,
                               @ConversationId,
                               @AuthorUserId,
                               @ReplyToMessageId,
                               @Content,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = message.Id.Value,
                ChannelId = channelId,
                ConversationId = conversationId,
                AuthorUserId = message.AuthorUserId.Value,
                ReplyToMessageId = message.ReplyToMessageId?.Value,
                Content = message.Content?.Value,
                message.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
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
                                  reply_to_message_id AS "ReplyToMessageId",
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

        return MessageRepositoryHelpers.MapToMessage(row);
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
                Content = message.Content?.Value,
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

    public async Task<ReplyTargetSummary?> GetReplyTargetSummaryAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT m.id AS "MessageId",
                                  m.channel_id AS "ChannelId",
                                  m.conversation_id AS "ConversationId",
                                  m.author_user_id AS "AuthorUserId",
                                  u.username AS "AuthorUsername",
                                  u.display_name AS "AuthorDisplayName",
                                  LEFT(COALESCE(m.content, ''), 200) AS "Content",
                                  m.deleted_at_utc IS NOT NULL AS "IsDeleted",
                                  m.deleted_at_utc AS "DeletedAtUtc",
                                  EXISTS(SELECT 1 FROM message_attachments ma WHERE ma.message_id = m.id) AS "HasAttachments"
                           FROM messages m
                           JOIN users u ON u.id = m.author_user_id
                           WHERE m.id = @MessageId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ReplyTargetSummaryRow>(command);
        if (row is null)
            return null;

        return new ReplyTargetSummary(
            MessageId.From(row.MessageId),
            MessageRepositoryHelpers.MapToScope(row.ChannelId, row.ConversationId),
            UserId.From(row.AuthorUserId),
            row.AuthorUsername,
            row.AuthorDisplayName,
            row.IsDeleted ? null : row.Content,
            row.HasAttachments,
            row.IsDeleted,
            row.DeletedAtUtc);
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

    public async Task AddMentionsAsync(
        MessageId messageId,
        IReadOnlyCollection<UserId> mentionedUserIds,
        CancellationToken cancellationToken = default)
    {
        if (mentionedUserIds.Count == 0)
            return;

        const string sql = """
                           INSERT INTO message_mentions (message_id, mentioned_user_id)
                           VALUES (@MessageId, @MentionedUserId)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        foreach (var userId in mentionedUserIds)
        {
            var command = new CommandDefinition(
                sql,
                new
                {
                    MessageId = messageId.Value,
                    MentionedUserId = userId.Value
                },
                transaction: _dbSession.Transaction,
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
    }

    public async Task ReplaceMentionsAsync(
        MessageId messageId,
        IReadOnlyCollection<UserId> mentionedUserIds,
        CancellationToken cancellationToken = default)
    {
        const string deleteSql = """
                                 DELETE FROM message_mentions
                                 WHERE message_id = @MessageId
                                 """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var deleteCommand = new CommandDefinition(
            deleteSql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(deleteCommand);

        if (mentionedUserIds.Count > 0)
        {
            await AddMentionsAsync(messageId, mentionedUserIds, cancellationToken);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetMentionedUserIdsByMessageIdAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Guid>>();

        const string sql = """
                           SELECT message_id, mentioned_user_id
                           FROM message_mentions
                           WHERE message_id = ANY(@MessageIds)
                           ORDER BY message_id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageIds = messageIds.ToArray() },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<(Guid messageId, Guid mentionedUserId)>(command);

        return rows
            .GroupBy(r => r.messageId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(r => r.mentionedUserId).Distinct().ToArray());
    }

}
