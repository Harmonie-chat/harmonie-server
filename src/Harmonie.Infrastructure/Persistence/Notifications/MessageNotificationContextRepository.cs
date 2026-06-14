using Dapper;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Notifications;

public sealed class MessageNotificationContextRepository : IMessageNotificationContextRepository
{
    private readonly DbSession _dbSession;

    public MessageNotificationContextRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<MessageNotificationContext?> GetAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default)
    {
        const string messageSql = """
                                  SELECT
                                      m.id AS "MessageId",
                                      m.author_user_id AS "AuthorUserId",
                                      u.username AS "AuthorUsername",
                                      u.display_name AS "AuthorDisplayName",
                                      m.channel_id AS "ChannelId",
                                      gc.guild_id AS "GuildId",
                                      g.name AS "GuildName",
                                      gc.name AS "ChannelName",
                                      m.conversation_id AS "ConversationId",
                                      c.name AS "ConversationName"
                                  FROM messages m
                                  JOIN users u ON u.id = m.author_user_id
                                  LEFT JOIN guild_channels gc ON gc.id = m.channel_id
                                  LEFT JOIN guilds g ON g.id = gc.guild_id
                                  LEFT JOIN conversations c ON c.id = m.conversation_id
                                  WHERE m.id = @MessageId
                                    AND m.deleted_at_utc IS NULL
                                  """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var messageCommand = new CommandDefinition(
            messageSql,
            new { MessageId = messageId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MessageNotificationContextRow>(messageCommand);
        if (row is null)
            return null;

        if (row.ChannelId is not null && row.GuildId is not null && row.GuildName is not null && row.ChannelName is not null)
        {
            var recipientRows = await QueryRecipientIdsAsync(
                """
                SELECT user_id
                FROM guild_members
                WHERE guild_id = @GuildId
                """,
                new { row.GuildId },
                cancellationToken);

            return new MessageNotificationContext(
                MessageId.From(row.MessageId),
                UserId.From(row.AuthorUserId),
                row.AuthorUsername,
                row.AuthorDisplayName,
                new MessageNotificationTarget.Channel(
                    GuildId.From(row.GuildId.Value),
                    row.GuildName,
                    GuildChannelId.From(row.ChannelId.Value),
                    row.ChannelName),
                recipientRows.Select(UserId.From).ToHashSet());
        }

        if (row.ConversationId is not null)
        {
            var recipientRows = await QueryRecipientIdsAsync(
                """
                SELECT user_id
                FROM conversation_participants
                WHERE conversation_id = @ConversationId
                """,
                new { row.ConversationId },
                cancellationToken);

            return new MessageNotificationContext(
                MessageId.From(row.MessageId),
                UserId.From(row.AuthorUserId),
                row.AuthorUsername,
                row.AuthorDisplayName,
                new MessageNotificationTarget.Conversation(ConversationId.From(row.ConversationId.Value), row.ConversationName),
                recipientRows.Select(UserId.From).ToHashSet());
        }

        return null;
    }

    private async Task<IReadOnlyList<Guid>> QueryRecipientIdsAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<Guid>(command);
        return rows.ToArray();
    }

    private sealed class MessageNotificationContextRow
    {
        public Guid MessageId { get; init; }

        public Guid AuthorUserId { get; init; }

        public string AuthorUsername { get; init; } = string.Empty;

        public string? AuthorDisplayName { get; init; }

        public Guid? ChannelId { get; init; }

        public Guid? GuildId { get; init; }

        public string? GuildName { get; init; }

        public string? ChannelName { get; init; }

        public Guid? ConversationId { get; init; }

        public string? ConversationName { get; init; }
    }
}
