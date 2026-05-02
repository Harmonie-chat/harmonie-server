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

internal static class MessageRepositoryHelpers
{
    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>>> GetAttachmentsByMessageIdsAsync(
        DbSession dbSession,
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

        var connection = await dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageIds = messageIds.ToArray() },
            transaction: dbSession.Transaction,
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

    internal static IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> BuildAttachmentsDictionary(
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

    internal static IReadOnlyDictionary<Guid, IReadOnlyList<MessageReactionSummary>> BuildReactionsDictionary(
        IEnumerable<ReactionSummaryRow> summaryRows,
        IEnumerable<ReactionUserRow> userRows)
    {
        var usersByKey = userRows
            .GroupBy(row => (row.MessageId, row.Emoji))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ReactionUser>)group
                    .Select(row => new ReactionUser(
                        row.UserId,
                        row.Username,
                        row.DisplayName))
                    .ToArray());

        return summaryRows
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MessageReactionSummary>)group
                    .Select(row =>
                    {
                        usersByKey.TryGetValue((row.MessageId, row.Emoji), out var users);
                        return new MessageReactionSummary(
                            row.Emoji,
                            row.Count,
                            row.ReactedByCaller,
                            users ?? Array.Empty<ReactionUser>());
                    })
                    .ToArray());
    }

    internal static Message MapToMessage(
        MessageRow row,
        IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachment>> attachmentsByMessageId)
    {
        MessageContent? messageContent = null;
        if (row.Content is not null)
        {
            var contentResult = MessageContent.Create(row.Content);
            if (contentResult.IsFailure || contentResult.Value is null)
                throw new InvalidOperationException("Stored message content is invalid.");
            messageContent = contentResult.Value;
        }

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
            messageContent,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.DeletedAtUtc,
            attachments);
    }

    internal static IReadOnlyDictionary<Guid, IReadOnlyList<LinkPreviewDto>> BuildLinkPreviewsDictionary(
        IEnumerable<MessageLinkPreviewRow> rows)
    {
        return rows
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LinkPreviewDto>)group
                    .Select(row => new LinkPreviewDto(
                        row.Url,
                        row.Title,
                        row.Description,
                        row.ImageUrl,
                        row.SiteName))
                    .ToArray());
    }
}
