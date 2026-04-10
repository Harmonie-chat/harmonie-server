using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Harmonie.Application.Features.Users.UploadMyAvatar;

public sealed record UploadMyAvatarInput(string FileName, string ContentType, Stream Content);

public sealed class UploadMyAvatarHandler
    : IAuthenticatedHandler<UploadMyAvatarInput, UploadMyAvatarResponse>
{
    private const int AvatarSize = 256;

    private readonly IUserRepository _userRepository;
    private readonly IUploadedFileRepository _uploadedFileRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserProfileNotifier _userProfileNotifier;
    private readonly ILogger<UploadMyAvatarHandler> _logger;

    public UploadMyAvatarHandler(
        IUserRepository userRepository,
        IUploadedFileRepository uploadedFileRepository,
        IObjectStorageService objectStorageService,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        IUserProfileNotifier userProfileNotifier,
        ILogger<UploadMyAvatarHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileRepository = uploadedFileRepository;
        _objectStorageService = objectStorageService;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _userProfileNotifier = userProfileNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UploadMyAvatarResponse>> HandleAsync(
        UploadMyAvatarInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User was not found");
        }

        using var resizedStream = await ResizeImageAsync(request.Content, request.ContentType, cancellationToken);
        var storageKey = BuildStorageKey(currentUserId, request.FileName);
        var previousAvatarFileId = user.AvatarFileId;

        var uploadedFileResult = UploadedFile.Create(
            currentUserId,
            request.FileName,
            request.ContentType,
            resizedStream.Length,
            storageKey,
            UploadPurpose.Avatar);

        if (uploadedFileResult.IsFailure || uploadedFileResult.Value is null)
        {
            return ApplicationResponse<UploadMyAvatarResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                uploadedFileResult.Error ?? "Uploaded file metadata is invalid");
        }

        var uploadResult = await _objectStorageService.UploadAsync(
            new ObjectStorageUploadRequest(
                storageKey,
                request.ContentType,
                resizedStream.Length,
                resizedStream),
            cancellationToken);

        if (!uploadResult.Success)
        {
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

        if (previousAvatarFileId is not null && previousAvatarFileId != uploadedFileResult.Value.Id)
            await _uploadedFileCleanupService.DeleteIfExistsAsync(previousAvatarFileId, cancellationToken);

        var notificationContext = await _userRepository.GetUserNotificationContextAsync(
            currentUserId, cancellationToken);

        var guildIds = notificationContext.GuildIds.Select(id => GuildId.From(id)).ToArray();
        var conversationIds = notificationContext.ConversationIds.Select(id => ConversationId.From(id)).ToArray();

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _userProfileNotifier.NotifyProfileUpdatedAsync(
                new UserProfileUpdatedNotification(
                    UserId: user.Id,
                    DisplayName: user.DisplayName,
                    AvatarFileId: user.AvatarFileId,
                    GuildIds: guildIds,
                    ConversationIds: conversationIds),
                ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to notify profile update for user {UserId}",
            user.Id);

        return ApplicationResponse<UploadMyAvatarResponse>.Ok(
            new UploadMyAvatarResponse(uploadedFileResult.Value.Id.Value));
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
