using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Uploads.DeleteFile;

public sealed record DeleteFileInput(UploadedFileId FileId);

public sealed class DeleteFileHandler : IAuthenticatedHandler<DeleteFileInput, bool>
{
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteFileHandler> _logger;

    public DeleteFileHandler(
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        IUnitOfWork unitOfWork,
        ILogger<DeleteFileHandler> logger)
    {
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteFileInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var uploadedFile = await _uploadedFileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (uploadedFile is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Upload.NotFound,
                "File was not found");
        }

        if (uploadedFile.UploaderUserId != currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Upload.AccessDenied,
                "You can only delete files you have uploaded");
        }

        var storageKey = uploadedFile.StorageKey;

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _uploadedFileRepository.DeleteAsync(request.FileId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await DeleteStoredObjectSafelyAsync(storageKey, request.FileId, cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task DeleteStoredObjectSafelyAsync(
        string storageKey,
        UploadedFileId fileId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _objectStorageService.DeleteIfExistsAsync(storageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "DeleteFile storage cleanup failed. UploadedFileId={UploadedFileId}, StorageKey={StorageKey}",
                fileId,
                storageKey);
        }
    }
}
