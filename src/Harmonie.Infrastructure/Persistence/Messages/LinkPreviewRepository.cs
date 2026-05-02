using Dapper;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Infrastructure.Persistence.Common;
using Harmonie.Infrastructure.Rows.Messages;

namespace Harmonie.Infrastructure.Persistence.Messages;

internal sealed class LinkPreviewRepository : ILinkPreviewRepository
{
    private readonly DbSession _dbSession;

    public LinkPreviewRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<IReadOnlyList<MessageLinkPreview>> GetByMessageIdsAsync(
        IReadOnlyCollection<MessageId> messageIds,
        CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
            return Array.Empty<MessageLinkPreview>();

        const string sql = """
                           SELECT message_id AS "MessageId",
                                  url AS "Url",
                                  title AS "Title",
                                  description AS "Description",
                                  image_url AS "ImageUrl",
                                  site_name AS "SiteName",
                                  fetched_at_utc AS "FetchedAtUtc"
                           FROM message_link_previews
                           WHERE message_id = ANY(@MessageIds)
                           ORDER BY message_id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { MessageIds = messageIds.Select(m => m.Value).ToArray() },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<MessageLinkPreviewRow>(command);

        return rows
            .Select(row => MessageLinkPreview.Rehydrate(
                MessageId.From(row.MessageId),
                row.Url,
                row.Title,
                row.Description,
                row.ImageUrl,
                row.SiteName,
                row.FetchedAtUtc))
            .ToArray();
    }

    public async Task<MessageLinkPreview?> TryGetRecentPreviewAsync(
        string url,
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT message_id AS "MessageId",
                                  url AS "Url",
                                  title AS "Title",
                                  description AS "Description",
                                  image_url AS "ImageUrl",
                                  site_name AS "SiteName",
                                  fetched_at_utc AS "FetchedAtUtc"
                           FROM message_link_previews
                           WHERE url = @Url
                             AND fetched_at_utc > @MinFetchedAt
                           ORDER BY fetched_at_utc DESC
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Url = url,
                MinFetchedAt = DateTime.UtcNow - maxAge
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MessageLinkPreviewRow>(command);
        if (row is null)
            return null;

        return MessageLinkPreview.Rehydrate(
            MessageId.From(row.MessageId),
            row.Url,
            row.Title,
            row.Description,
            row.ImageUrl,
            row.SiteName,
            row.FetchedAtUtc);
    }

    public async Task AddAsync(
        MessageLinkPreview preview,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO message_link_previews (
                               message_id,
                               url,
                               title,
                               description,
                               image_url,
                               site_name,
                               fetched_at_utc)
                           VALUES (
                               @MessageId,
                               @Url,
                               @Title,
                               @Description,
                               @ImageUrl,
                               @SiteName,
                               @FetchedAtUtc)
                           ON CONFLICT (message_id, url) DO NOTHING
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = preview.MessageId.Value,
                Url = preview.Url,
                Title = preview.Title,
                Description = preview.Description,
                ImageUrl = preview.ImageUrl,
                SiteName = preview.SiteName,
                FetchedAtUtc = preview.FetchedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
