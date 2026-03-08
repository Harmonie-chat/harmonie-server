using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed class UploadFileHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadFileHandler> _logger;

    public UploadFileHandler(
        IUserRepository userRepository,
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        IUnitOfWork unitOfWork,
        ILogger<UploadFileHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UploadFileResponse>> HandleAsync(
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UploadFile started. UserId={UserId}, FileName={FileName}, ContentType={ContentType}, SizeBytes={SizeBytes}",
            currentUserId,
            fileName,
            contentType,
            sizeBytes);

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning(
                "UploadFile failed because uploader was not found. UserId={UserId}",
                currentUserId);

            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "Uploader was not found");
        }

        var storageKey = BuildStorageKey(fileName);
        var uploadFileResult = UploadedFile.Create(
            currentUserId,
            fileName,
            contentType,
            sizeBytes,
            storageKey);

        if (uploadFileResult.IsFailure || uploadFileResult.Value is null)
        {
            _logger.LogWarning(
                "UploadFile domain validation failed. UserId={UserId}, FileName={FileName}, Error={Error}",
                currentUserId,
                fileName,
                uploadFileResult.Error);

            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                uploadFileResult.Error ?? "Uploaded file metadata is invalid");
        }

        var uploadResult = await _objectStorageService.UploadAsync(
            new ObjectStorageUploadRequest(
                uploadFileResult.Value.StorageKey,
                uploadFileResult.Value.ContentType,
                uploadFileResult.Value.SizeBytes,
                content),
            cancellationToken);

        if (!uploadResult.Success)
        {
            _logger.LogWarning(
                "UploadFile failed while storing object. UserId={UserId}, StorageKey={StorageKey}, Reason={Reason}",
                currentUserId,
                uploadFileResult.Value.StorageKey,
                uploadResult.FailureReason);

            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.Upload.StorageUnavailable,
                uploadResult.FailureReason ?? "Object storage upload failed");
        }

        try
        {
            await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
            {
                await _uploadedFileRepository.AddAsync(uploadFileResult.Value, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await DeleteStoredObjectSafelyAsync(uploadFileResult.Value.StorageKey, cancellationToken);
            throw;
        }

        var payload = new UploadFileResponse(
            FileId: uploadFileResult.Value.Id.ToString(),
            Url: _objectStorageService.BuildPublicUrl(uploadFileResult.Value.StorageKey),
            Filename: uploadFileResult.Value.FileName,
            ContentType: uploadFileResult.Value.ContentType,
            SizeBytes: uploadFileResult.Value.SizeBytes);

        _logger.LogInformation(
            "UploadFile succeeded. FileId={FileId}, UserId={UserId}, StorageKey={StorageKey}",
            uploadFileResult.Value.Id,
            currentUserId,
            uploadFileResult.Value.StorageKey);

        return ApplicationResponse<UploadFileResponse>.Ok(payload);
    }

    private static string BuildStorageKey(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var now = DateTime.UtcNow;
        return $"uploads/{now:yyyy/MM}/{Guid.NewGuid():N}{extension}";
    }

    private async Task DeleteStoredObjectSafelyAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _objectStorageService.DeleteIfExistsAsync(storageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "UploadFile cleanup failed. StorageKey={StorageKey}",
                storageKey);
        }
    }
}
