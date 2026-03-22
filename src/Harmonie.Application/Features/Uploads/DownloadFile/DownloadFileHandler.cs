using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Uploads.DownloadFile;

public sealed class DownloadFileHandler : IAuthenticatedHandler<UploadedFileId, DownloadFileResult>
{
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;

    public DownloadFileHandler(
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService)
    {
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
    }

    public async Task<ApplicationResponse<DownloadFileResult>> HandleAsync(
        UploadedFileId fileId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var uploadedFile = await _uploadedFileRepository.GetByIdAsync(fileId, cancellationToken);
        if (uploadedFile is null)
        {
            return ApplicationResponse<DownloadFileResult>.Fail(
                ApplicationErrorCodes.Upload.NotFound,
                "File was not found");
        }

        var stream = await _objectStorageService.GetStreamAsync(
            uploadedFile.StorageKey,
            cancellationToken);

        if (stream is null)
        {
            return ApplicationResponse<DownloadFileResult>.Fail(
                ApplicationErrorCodes.Upload.StorageUnavailable,
                "File content is unavailable");
        }

        return ApplicationResponse<DownloadFileResult>.Ok(
            new DownloadFileResult(
                stream,
                uploadedFile.ContentType,
                uploadedFile.FileName));
    }
}
