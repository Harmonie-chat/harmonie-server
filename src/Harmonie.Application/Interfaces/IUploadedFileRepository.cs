using Harmonie.Domain.Entities;

namespace Harmonie.Application.Interfaces;

public interface IUploadedFileRepository
{
    Task AddAsync(
        UploadedFile uploadedFile,
        CancellationToken cancellationToken = default);
}
