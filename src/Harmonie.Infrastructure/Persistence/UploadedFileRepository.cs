using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Infrastructure.Persistence;

public sealed class UploadedFileRepository : IUploadedFileRepository
{
    private readonly DbSession _dbSession;

    public UploadedFileRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<UploadedFile?> GetByIdAsync(
        UploadedFileId id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT
                               id AS "Id",
                               uploader_id AS "UploaderId",
                               filename AS "Filename",
                               content_type AS "ContentType",
                               size_bytes AS "SizeBytes",
                               storage_key AS "StorageKey",
                               purpose AS "Purpose",
                               created_at_utc AS "CreatedAtUtc"
                           FROM uploaded_files
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Id = id.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UploadedFileRow>(command);
        if (row is null)
            return null;

        return UploadedFile.Rehydrate(
            UploadedFileId.From(row.Id),
            UserId.From(row.UploaderId),
            row.Filename,
            row.ContentType,
            row.SizeBytes,
            row.StorageKey,
            Enum.Parse<UploadPurpose>(row.Purpose, ignoreCase: true),
            row.CreatedAtUtc);
    }

    public async Task AddAsync(
        UploadedFile uploadedFile,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO uploaded_files (
                               id,
                               uploader_id,
                               filename,
                               content_type,
                               size_bytes,
                               storage_key,
                               purpose,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @UploaderId,
                               @Filename,
                               @ContentType,
                               @SizeBytes,
                               @StorageKey,
                               @Purpose,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = uploadedFile.Id.Value,
                UploaderId = uploadedFile.UploaderUserId.Value,
                Filename = uploadedFile.FileName,
                ContentType = uploadedFile.ContentType,
                SizeBytes = uploadedFile.SizeBytes,
                StorageKey = uploadedFile.StorageKey,
                Purpose = uploadedFile.Purpose.ToString().ToLowerInvariant(),
                uploadedFile.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private sealed class UploadedFileRow
    {
        public Guid Id { get; init; }
        public Guid UploaderId { get; init; }
        public string Filename { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public string StorageKey { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
    }
}
