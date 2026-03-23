using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Users.DeleteMyAvatar;

public sealed class DeleteMyAvatarHandler : IAuthenticatedHandler<Unit, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMyAvatarHandler(
        IUserRepository userRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        Unit request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User was not found");
        }

        var previousAvatarFileId = user.AvatarFileId;
        if (previousAvatarFileId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Upload.NotFound,
                "User avatar was not found");
        }

        var avatarUpdateResult = user.UpdateAvatarFile(null);
        if (avatarUpdateResult.IsFailure)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                avatarUpdateResult.Error ?? "Avatar file is invalid");
        }

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _userRepository.UpdateProfileAsync(
                new ProfileUpdateParameters(
                    UserId: user.Id,
                    DisplayNameIsSet: false, DisplayName: null,
                    BioIsSet: false, Bio: null,
                    AvatarFileIdIsSet: true, AvatarFileId: null,
                    AvatarColorIsSet: false, AvatarColor: null,
                    AvatarIconIsSet: false, AvatarIcon: null,
                    AvatarBgIsSet: false, AvatarBg: null,
                    ThemeIsSet: false, Theme: null,
                    LanguageIsSet: false, Language: null,
                    UpdatedAtUtc: user.UpdatedAtUtc),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(previousAvatarFileId, cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
