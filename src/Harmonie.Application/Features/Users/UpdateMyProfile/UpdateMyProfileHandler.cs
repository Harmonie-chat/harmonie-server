using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileHandler
    : IAuthenticatedHandler<UpdateMyProfileRequest, UpdateMyProfileResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUserProfileNotifier _userProfileNotifier;
    private readonly ILogger<UpdateMyProfileHandler> _logger;

    public UpdateMyProfileHandler(
        IUserRepository userRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUserProfileNotifier userProfileNotifier,
        ILogger<UpdateMyProfileHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _userProfileNotifier = userProfileNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UpdateMyProfileResponse>> HandleAsync(
        UpdateMyProfileRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<UpdateMyProfileResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User profile was not found");
        }

        var previousAvatarFileId = user.AvatarFileId;

        if (request.DisplayNameIsSet)
        {
            var result = user.UpdateDisplayName(request.DisplayName);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.DisplayName), result);
        }

        if (request.BioIsSet)
        {
            var result = user.UpdateBio(request.Bio);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.Bio), result);
        }

        if (request.AvatarFileIdIsSet)
        {
            var avatarFileId = request.AvatarFileId.HasValue ? UploadedFileId.From(request.AvatarFileId.Value) : null;
            var result = user.UpdateAvatarFile(avatarFileId);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.AvatarFileId), result);
        }

        if (request.AvatarColorIsSet)
        {
            var result = user.UpdateAvatarColor(request.AvatarColor);
            if (result.IsFailure)
                return BuildValidationFailure("Avatar.Color", result);
        }

        if (request.AvatarIconIsSet)
        {
            var result = user.UpdateAvatarIcon(request.AvatarIcon);
            if (result.IsFailure)
                return BuildValidationFailure("Avatar.Icon", result);
        }

        if (request.AvatarBgIsSet)
        {
            var result = user.UpdateAvatarBg(request.AvatarBg);
            if (result.IsFailure)
                return BuildValidationFailure("Avatar.Bg", result);
        }

        if (request.ThemeIsSet)
        {
            if (request.Theme is null)
            {
                return BuildValidationFailure(
                    nameof(request.Theme),
                    "Theme cannot be null");
            }

            var result = user.UpdateTheme(request.Theme);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.Theme), result);
        }

        if (request.LanguageIsSet)
        {
            var result = user.UpdateLanguage(request.Language);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.Language), result);
        }

        var anyFieldSet = request.DisplayNameIsSet || request.BioIsSet || request.AvatarFileIdIsSet
            || request.AvatarColorIsSet || request.AvatarIconIsSet || request.AvatarBgIsSet
            || request.ThemeIsSet || request.LanguageIsSet;
        var shouldNotifyProfile = request.DisplayNameIsSet || request.BioIsSet || request.AvatarFileIdIsSet;
        var shouldDeletePreviousAvatar = request.AvatarFileIdIsSet
            && previousAvatarFileId is not null
            && previousAvatarFileId != user.AvatarFileId;

        if (anyFieldSet)
            await _userRepository.UpdateAsync(user, cancellationToken);

        if (shouldNotifyProfile)
        {
            var notificationContext = await _userRepository.GetUserNotificationContextAsync(
                currentUserId, cancellationToken);

            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _userProfileNotifier.NotifyProfileUpdatedAsync(
                    new UserProfileUpdatedNotification(
                        UserId: user.Id,
                        DisplayName: user.DisplayName,
                        AvatarFileId: user.AvatarFileId,
                        GuildIds: notificationContext.GuildIds,
                        ConversationIds: notificationContext.ConversationIds),
                    ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to notify profile update for user {UserId}",
                user.Id);
        }

        if (shouldDeletePreviousAvatar)
            await _uploadedFileCleanupService.DeleteIfExistsAsync(previousAvatarFileId, cancellationToken);

        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        var payload = new UpdateMyProfileResponse(
            UserId: user.Id.Value,
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarFileId: user.AvatarFileId?.Value,
            Avatar: avatar,
            Theme: user.Theme,
            Language: user.Language);

        return ApplicationResponse<UpdateMyProfileResponse>.Ok(payload);
    }

    private static ApplicationResponse<UpdateMyProfileResponse> BuildValidationFailure(
        string propertyName,
        string detail)
    {
        return ApplicationResponse<UpdateMyProfileResponse>.Fail(
            ApplicationErrorCodes.Common.ValidationFailed,
            "Request validation failed",
            EndpointExtensions.SingleValidationError(
                propertyName,
                ApplicationErrorCodes.Validation.Invalid,
                detail));
    }

    private static ApplicationResponse<UpdateMyProfileResponse> BuildValidationFailure(
        string propertyName,
        Result result)
    {
        return ApplicationResponse<UpdateMyProfileResponse>.Fail(
            ApplicationErrorCodes.Common.ValidationFailed,
            "Request validation failed",
            EndpointExtensions.SingleValidationError(
                propertyName,
                ApplicationErrorCodes.Validation.Invalid,
                result.Error ?? "Profile field is invalid"));
    }

}
