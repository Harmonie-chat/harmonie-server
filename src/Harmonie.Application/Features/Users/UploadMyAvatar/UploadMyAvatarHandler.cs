using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Harmonie.Application.Features.Users.UploadMyAvatar;

public sealed class UploadMyAvatarHandler
{
    private const int AvatarSize = 256;

    private readonly IUserRepository _userRepository;
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadMyAvatarHandler> _logger;

    public UploadMyAvatarHandler(
        IUserRepository userRepository,
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        ILogger<UploadMyAvatarHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UploadMyAvatarResponse>> HandleAsync(
        string fileName,
        string contentType,
        Stream content,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UploadMyAvatar started. UserId={UserId}, FileName={FileName}, ContentType={ContentType}",
            currentUserId,
            fileName,
            contentType);

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning(
                "UploadMyAvatar failed because user was not found. UserId={UserId}",
                currentUserId);

            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User was not found");
        }

        using var resizedStream = await ResizeImageAsync(content, contentType, cancellationToken);
        var storageKey = BuildStorageKey(currentUserId, fileName);
        var previousAvatarFileId = user.AvatarFileId;

        var uploadedFileResult = UploadedFile.Create(
            currentUserId,
            fileName,
            contentType,
            resizedStream.Length,
            storageKey,
            UploadPurpose.Avatar);

        if (uploadedFileResult.IsFailure || uploadedFileResult.Value is null)
        {
            _logger.LogWarning(
                "UploadMyAvatar domain validation failed. UserId={UserId}, FileName={FileName}, Error={Error}",
                currentUserId,
                fileName,
                uploadedFileResult.Error);

            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                uploadedFileResult.Error ?? "Uploaded file metadata is invalid");
        }

        var uploadResult = await _objectStorageService.UploadAsync(
            new ObjectStorageUploadRequest(
                storageKey,
                contentType,
                resizedStream.Length,
                resizedStream),
            cancellationToken);

        if (!uploadResult.Success)
        {
            _logger.LogWarning(
                "UploadMyAvatar failed while storing object. UserId={UserId}, StorageKey={StorageKey}, Reason={Reason}",
                currentUserId,
                storageKey,
                uploadResult.FailureReason);

            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.Upload.StorageUnavailable,
                uploadResult.FailureReason ?? "Object storage upload failed");
        }

        var avatarUpdateResult = user.UpdateAvatarFile(uploadedFileResult.Value.Id);
        if (avatarUpdateResult.IsFailure)
        {
            await DeleteStoredObjectSafelyAsync(storageKey, cancellationToken);
            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                avatarUpdateResult.Error ?? "Avatar file is invalid");
        }

        try
        {
            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            await _uploadedFileRepository.AddAsync(uploadedFileResult.Value, cancellationToken);
            await _userRepository.UpdateProfileAsync(
                new ProfileUpdateParameters(
                    UserId: user.Id,
                    DisplayNameIsSet: false, DisplayName: null,
                    BioIsSet: false, Bio: null,
                    AvatarFileIdIsSet: true, AvatarFileId: uploadedFileResult.Value.Id,
                    AvatarColorIsSet: false, AvatarColor: null,
                    AvatarIconIsSet: false, AvatarIcon: null,
                    AvatarBgIsSet: false, AvatarBg: null,
                    ThemeIsSet: false, Theme: null,
                    LanguageIsSet: false, Language: null,
                    UpdatedAtUtc: user.UpdatedAtUtc),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await DeleteStoredObjectSafelyAsync(storageKey, cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "UploadMyAvatar succeeded. UserId={UserId}, StorageKey={StorageKey}",
            currentUserId,
            storageKey);

        if (previousAvatarFileId is not null && previousAvatarFileId != uploadedFileResult.Value.Id)
            await _uploadedFileCleanupService.DeleteIfExistsAsync(previousAvatarFileId, cancellationToken);

        return ApplicationResponse<UploadMyAvatarResponse>.Ok(
            new UploadMyAvatarResponse(uploadedFileResult.Value.Id.ToString()));
    }

    private static async Task<MemoryStream> ResizeImageAsync(
        Stream source,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(source, cancellationToken);

        image.Mutate(ctx => ctx
            .Resize(new ResizeOptions
            {
                Size = new Size(AvatarSize, AvatarSize),
                Mode = ResizeMode.Crop
            }));

        var output = new MemoryStream();
        var encoder = ResolveEncoder(contentType);
        await image.SaveAsync(output, encoder, cancellationToken);
        output.Position = 0;
        return output;
    }

    private static SixLabors.ImageSharp.Formats.IImageEncoder ResolveEncoder(string contentType)
        => contentType.ToLowerInvariant() switch
        {
            "image/png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
            "image/webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder(),
            _ => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 }
        };

    private static string BuildStorageKey(UserId userId, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return $"avatars/{userId}/{Guid.NewGuid():N}{extension}";
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
                "UploadMyAvatar cleanup failed. StorageKey={StorageKey}",
                storageKey);
        }
    }
}
