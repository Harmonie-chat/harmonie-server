using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Users.DeleteMyAvatar;

public sealed class DeleteMyAvatarHandler : IAuthenticatedHandler<Unit, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserProfileNotifier _userProfileNotifier;
    private readonly ILogger<DeleteMyAvatarHandler> _logger;

    public DeleteMyAvatarHandler(
        IUserRepository userRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        IUserProfileNotifier userProfileNotifier,
        ILogger<DeleteMyAvatarHandler> logger)
    {
        _userRepository = userRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _userProfileNotifier = userProfileNotifier;
        _logger = logger;
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
            await _userRepository.UpdateAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(previousAvatarFileId, cancellationToken);

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

        return ApplicationResponse<bool>.Ok(true);
    }
}
