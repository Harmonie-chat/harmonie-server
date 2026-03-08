using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;

namespace Harmonie.Infrastructure.Persistence;

public sealed class UploadedFileRepository : IUploadedFileRepository
{
    private readonly DbSession _dbSession;

    public UploadedFileRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
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
                               created_at_utc)
                           VALUES (
                               @Id,
                               @UploaderId,
                               @Filename,
                               @ContentType,
                               @SizeBytes,
                               @StorageKey,
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
                uploadedFile.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
