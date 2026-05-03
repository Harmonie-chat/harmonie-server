using System.Data;
using Dapper;
using Harmonie.Application.Common.Messages;
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
                          m.content AS ""Content"",
                          m.created_at_utc AS ""CreatedAtUtc"",
                          m.updated_at_utc AS ""UpdatedAtUtc"",
                          m.deleted_at_utc AS ""DeletedAtUtc"",
                          pm.pinned_by_user_id AS ""PinnedByUserId"",
                          pm.pinned_at_utc AS ""PinnedAtUtc""
                   FROM pinned_messages pm
                   INNER JOIN messages m ON m.id = pm.message_id
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

        var messageIds = pageRows.Select(r => r.Id).ToArray();

        var attachmentsByMessageId = await MessageRepositoryHelpers.GetAttachmentsByMessageIdsAsync(
            _dbSession, messageIds, cancellationToken);

        var (reactionsByMessageId, linkPreviewsByMessageId) = await GetReactionsAndLinkPreviewsAsync(
            messageIds, callerId, cancellationToken);

        var items = pageRows
            .Select(row =>
            {
                attachmentsByMessageId.TryGetValue(row.Id, out var attachments);
                reactionsByMessageId.TryGetValue(row.Id, out var reactions);
                linkPreviewsByMessageId.TryGetValue(row.Id, out var linkPreviews);

                return new PinnedMessageSummary(
                    MessageId: row.Id,
                    AuthorUserId: row.AuthorUserId,
                    Content: row.DeletedAtUtc is null ? row.Content : null,
                    Attachments: attachments?.Select(a => new MessageAttachmentDto(
                        a.FileId.Value, a.FileName, a.ContentType, a.SizeBytes)).ToArray()
                        ?? Array.Empty<MessageAttachmentDto>(),
                    Reactions: reactions?.Select(r => new MessageReactionDto(
                        r.Emoji, r.Count, r.ReactedByCaller,
                        r.Users.Select(u => new ReactionUserDto(u.UserId, u.Username, u.DisplayName)).ToArray())).ToArray()
                        ?? Array.Empty<MessageReactionDto>(),
                    LinkPreviews: linkPreviews?.ToArray(),
                    CreatedAtUtc: row.CreatedAtUtc,
                    UpdatedAtUtc: row.UpdatedAtUtc,
                    PinnedByUserId: row.PinnedByUserId,
                    PinnedAtUtc: row.PinnedAtUtc);
            })
            .ToArray();

        PinnedMessagesCursor? nextCursor = null;
        if (hasMore && pageRows.Length > 0)
        {
            var lastRow = pageRows[^1];
            nextCursor = new PinnedMessagesCursor(lastRow.PinnedAtUtc, lastRow.Id);
        }

        return new PinnedMessagesPage(items, nextCursor);
    }

    private async Task<(IReadOnlyDictionary<Guid, IReadOnlyList<MessageReactionSummary>> Reactions,
        IReadOnlyDictionary<Guid, IReadOnlyList<LinkPreviewDto>> LinkPreviews)> GetReactionsAndLinkPreviewsAsync(
        Guid[] messageIds,
        UserId callerId,
        CancellationToken cancellationToken)
    {
        if (messageIds.Length == 0)
            return (new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
                    new Dictionary<Guid, IReadOnlyList<LinkPreviewDto>>());

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);

        var sql = $@"
                   SELECT message_id AS ""MessageId"",
                          emoji AS ""Emoji"",
                          COUNT(*) AS ""Count"",
                          BOOL_OR(user_id = @CallerId) AS ""ReactedByCaller""
                   FROM message_reactions
                   WHERE message_id = ANY(@MessageIds)
                   GROUP BY message_id, emoji;

                   SELECT mr.message_id AS ""MessageId"",
                          mr.emoji AS ""Emoji"",
                          u.id AS ""UserId"",
                          u.username AS ""Username"",
                          u.display_name AS ""DisplayName""
                   FROM message_reactions mr
                   JOIN users u ON u.id = mr.user_id
                   WHERE mr.message_id = ANY(@MessageIds)
                   ORDER BY mr.message_id, mr.emoji, mr.created_at_utc;

                   SELECT message_id AS ""MessageId"",
                          url AS ""Url"",
                          title AS ""Title"",
                          description AS ""Description"",
                          image_url AS ""ImageUrl"",
                          site_name AS ""SiteName""
                   FROM message_link_previews
                   WHERE message_id = ANY(@MessageIds);
                   ";

        var parameters = new DynamicParameters();
        parameters.Add("MessageIds", messageIds);
        parameters.Add("CallerId", callerId.Value);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);

        var reactionSummaries = (await multi.ReadAsync<ReactionSummaryRow>()).ToArray();
        var reactionUsers = (await multi.ReadAsync<ReactionUserRow>()).ToArray();
        var linkPreviewRows = (await multi.ReadAsync<MessageLinkPreviewRow>()).ToArray();

        var reactions = MessageRepositoryHelpers.BuildReactionsDictionary(reactionSummaries, reactionUsers);
        var linkPreviews = MessageRepositoryHelpers.BuildLinkPreviewsDictionary(linkPreviewRows);

        return (reactions, linkPreviews);
    }
}
