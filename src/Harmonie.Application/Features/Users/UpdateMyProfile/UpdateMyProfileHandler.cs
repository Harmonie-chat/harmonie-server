using Harmonie.Application.Common;
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
            var displayNameUpdateResult = user.UpdateDisplayName(request.DisplayName);
            if (displayNameUpdateResult.IsFailure)
                return BuildValidationFailure(nameof(request.DisplayName), displayNameUpdateResult);
        }

        if (request.BioIsSet)
        {
            var bioUpdateResult = user.UpdateBio(request.Bio);
            if (bioUpdateResult.IsFailure)
                return BuildValidationFailure(nameof(request.Bio), bioUpdateResult);
        }

        if (request.AvatarUrlIsSet)
        {
            var avatarUrlUpdateResult = user.UpdateAvatar(request.AvatarUrl);
            if (avatarUrlUpdateResult.IsFailure)
                return BuildValidationFailure(nameof(request.AvatarUrl), avatarUrlUpdateResult);
        }

        if (request.DisplayNameIsSet || request.BioIsSet || request.AvatarUrlIsSet)
            await _userRepository.UpdateAsync(user, cancellationToken);

        var payload = new UpdateMyProfileResponse(
            UserId: user.Id.ToString(),
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarUrl: user.AvatarUrl);

        _logger.LogInformation(
            "UpdateMyProfile succeeded for user {UserId}",
            currentUserId);

        return ApplicationResponse<UpdateMyProfileResponse>.Ok(payload);
    }

    private static ApplicationResponse<UpdateMyProfileResponse> BuildValidationFailure(
        string propertyName,
        Result result)
    {
        var details = new Dictionary<string, string[]>
        {
            [propertyName] = [result.Error ?? "Profile field is invalid"]
        };

        return ApplicationResponse<UpdateMyProfileResponse>.Fail(
            ApplicationErrorCodes.Common.ValidationFailed,
            "Request validation failed",
            details);
    }
}
