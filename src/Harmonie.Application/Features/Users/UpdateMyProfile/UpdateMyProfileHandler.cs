using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileHandler
{
    private readonly IUserRepository _userRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly ILogger<UpdateMyProfileHandler> _logger;

    public UpdateMyProfileHandler(
        IUserRepository userRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        ILogger<UpdateMyProfileHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UpdateMyProfileResponse>> HandleAsync(
        UpdateMyProfileRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UpdateMyProfile started for user {UserId}",
            currentUserId);

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning(
                "UpdateMyProfile failed because user was not found. UserId={UserId}",
                currentUserId);

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
            if (!TryParseUploadedFileId(request.AvatarFileId, out var avatarFileId))
            {
                return BuildValidationFailure(
                    nameof(request.AvatarFileId),
                    "Avatar file ID is invalid");
            }

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
        var shouldDeletePreviousAvatar = request.AvatarFileIdIsSet
            && previousAvatarFileId is not null
            && previousAvatarFileId != user.AvatarFileId;

        if (anyFieldSet)
        {
            var parameters = new ProfileUpdateParameters(
                UserId: user.Id,
                DisplayNameIsSet: request.DisplayNameIsSet,
                DisplayName: request.DisplayName,
                BioIsSet: request.BioIsSet,
                Bio: request.Bio,
                AvatarFileIdIsSet: request.AvatarFileIdIsSet,
                AvatarFileId: user.AvatarFileId,
                AvatarColorIsSet: request.AvatarColorIsSet,
                AvatarColor: request.AvatarColor,
                AvatarIconIsSet: request.AvatarIconIsSet,
                AvatarIcon: request.AvatarIcon,
                AvatarBgIsSet: request.AvatarBgIsSet,
                AvatarBg: request.AvatarBg,
                ThemeIsSet: request.ThemeIsSet,
                Theme: request.Theme,
                LanguageIsSet: request.LanguageIsSet,
                Language: request.Language,
                UpdatedAtUtc: user.UpdatedAtUtc);

            await _userRepository.UpdateProfileAsync(parameters, cancellationToken);
        }

        if (shouldDeletePreviousAvatar)
            await _uploadedFileCleanupService.DeleteIfExistsAsync(previousAvatarFileId, cancellationToken);

        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        var payload = new UpdateMyProfileResponse(
            UserId: user.Id.ToString(),
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarFileId: user.AvatarFileId?.ToString(),
            Avatar: avatar,
            Theme: user.Theme,
            Language: user.Language);

        _logger.LogInformation(
            "UpdateMyProfile succeeded for user {UserId}",
            currentUserId);

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

    private static bool TryParseUploadedFileId(string? fileId, out UploadedFileId? uploadedFileId)
    {
        if (fileId is null)
        {
            uploadedFileId = null;
            return true;
        }

        return UploadedFileId.TryParse(fileId, out uploadedFileId);
    }
}
