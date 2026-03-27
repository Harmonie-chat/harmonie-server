using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Users.GetMyProfile;

public sealed class GetMyProfileHandler : IAuthenticatedHandler<Unit, GetMyProfileResponse>
{
    private readonly IUserRepository _userRepository;

    public GetMyProfileHandler(
        IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ApplicationResponse<GetMyProfileResponse>> HandleAsync(
        Unit request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<GetMyProfileResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User profile was not found");
        }

        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        var payload = new GetMyProfileResponse(
            UserId: user.Id.Value,
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarFileId: user.AvatarFileId?.Value,
            Avatar: avatar,
            Theme: user.Theme,
            Language: user.Language,
            Status: user.Status);

        return ApplicationResponse<GetMyProfileResponse>.Ok(payload);
    }
}
