using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IUploadedFileRepository
{
    Task<UploadedFile?> GetByIdAsync(
        UploadedFileId id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        UploadedFile uploadedFile,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        UploadedFileId id,
        CancellationToken cancellationToken = default);
}
