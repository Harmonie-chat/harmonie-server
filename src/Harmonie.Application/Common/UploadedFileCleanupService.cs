using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Common;

public sealed class UploadedFileCleanupService
{
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly ILogger<UploadedFileCleanupService> _logger;

    public UploadedFileCleanupService(
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        ILogger<UploadedFileCleanupService> logger)
    {
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _logger = logger;
    }

    public async Task DeleteIfExistsAsync(
        UploadedFileId? uploadedFileId,
        CancellationToken cancellationToken = default)
    {
        if (uploadedFileId is null)
            return;

        var uploadedFile = await _uploadedFileRepository.GetByIdAsync(uploadedFileId, cancellationToken);
        if (uploadedFile is null)
            return;

        try
        {
            await _objectStorageService.DeleteIfExistsAsync(uploadedFile.StorageKey, cancellationToken);
            await _uploadedFileRepository.DeleteAsync(uploadedFileId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Uploaded file cleanup failed. UploadedFileId={UploadedFileId}, StorageKey={StorageKey}",
                uploadedFileId,
                uploadedFile.StorageKey);
        }
    }
}
