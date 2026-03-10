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
    private readonly ILogger<UpdateMyProfileHandler> _logger;

    public UpdateMyProfileHandler(
        IUserRepository userRepository,
        ILogger<UpdateMyProfileHandler> logger)
    {
        _userRepository = userRepository;
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

        if (request.AvatarUrlIsSet)
        {
            var result = user.UpdateAvatar(request.AvatarUrl);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.AvatarUrl), result);
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
            var result = user.UpdateTheme(request.Theme!);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.Theme), result);
        }

        if (request.LanguageIsSet)
        {
            var result = user.UpdateLanguage(request.Language);
            if (result.IsFailure)
                return BuildValidationFailure(nameof(request.Language), result);
        }

        var anyFieldSet = request.DisplayNameIsSet || request.BioIsSet || request.AvatarUrlIsSet
            || request.AvatarColorIsSet || request.AvatarIconIsSet || request.AvatarBgIsSet
            || request.ThemeIsSet || request.LanguageIsSet;

        if (anyFieldSet)
        {
            var parameters = new ProfileUpdateParameters(
                UserId: user.Id,
                DisplayNameIsSet: request.DisplayNameIsSet,
                DisplayName: request.DisplayName,
                BioIsSet: request.BioIsSet,
                Bio: request.Bio,
                AvatarUrlIsSet: request.AvatarUrlIsSet,
                AvatarUrl: request.AvatarUrl,
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

        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        var payload = new UpdateMyProfileResponse(
            UserId: user.Id.ToString(),
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarUrl: user.AvatarUrl,
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
