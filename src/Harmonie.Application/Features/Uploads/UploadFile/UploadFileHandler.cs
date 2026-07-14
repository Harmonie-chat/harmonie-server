using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed record UploadFileInput(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    UploadPurpose Purpose);

public sealed class UploadFileHandler : IAuthenticatedHandler<UploadFileInput, UploadFileResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UploadFileHandler> _logger;

    public UploadFileHandler(
        IUserRepository userRepository,
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<UploadFileHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UploadFileResponse>> HandleAsync(
        UploadFileInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "Uploader was not found");
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var storageKey = BuildStorageKey(request.FileName, nowUtc);
        var uploadFileResult = UploadedFile.Create(
            currentUserId,
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            storageKey,
            request.Purpose,
            nowUtc);

        if (uploadFileResult.IsFailure || uploadFileResult.Value is null)
        {
            return ApplicationResponse<UploadFileResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                uploadFileResult.Error ?? "Uploaded file metadata is invalid");
        }

        var uploadResult = await _objectStorageService.UploadAsync(
            new ObjectStorageUploadRequest(
                uploadFileResult.Value.StorageKey,
                uploadFileResult.Value.ContentType,
                uploadFileResult.Value.SizeBytes,
                request.Content),
            cancellationToken);

        if (!uploadResult.Success)
        {
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
            FileId: uploadFileResult.Value.Id.Value,
            Filename: uploadFileResult.Value.FileName,
            ContentType: uploadFileResult.Value.ContentType,
            SizeBytes: uploadFileResult.Value.SizeBytes);

        return ApplicationResponse<UploadFileResponse>.Ok(payload);
    }

    private static string BuildStorageKey(string fileName, DateTime createdAtUtc)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return $"uploads/{createdAtUtc:yyyy/MM}/{Guid.NewGuid():N}{extension}";
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
