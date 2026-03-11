using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Users.GetMyProfile;

public sealed class GetMyProfileHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetMyProfileHandler> _logger;

    public GetMyProfileHandler(
        IUserRepository userRepository,
        ILogger<GetMyProfileHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<GetMyProfileResponse>> HandleAsync(
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetMyProfile started for user {UserId}",
            currentUserId);

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning(
                "GetMyProfile user not found. UserId={UserId}",
                currentUserId);

            return ApplicationResponse<GetMyProfileResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User profile was not found");
        }

        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        var payload = new GetMyProfileResponse(
            UserId: user.Id.ToString(),
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarFileId: user.AvatarFileId?.ToString(),
            Avatar: avatar,
            Theme: user.Theme,
            Language: user.Language);

        _logger.LogInformation(
            "GetMyProfile succeeded for user {UserId}",
            currentUserId);

        return ApplicationResponse<GetMyProfileResponse>.Ok(payload);
    }
}
