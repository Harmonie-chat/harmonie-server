using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Users.UpdateUserStatus;

public sealed class UpdateUserStatusHandler
    : IAuthenticatedHandler<UpdateUserStatusRequest, UpdateUserStatusResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUserPresenceNotifier _userPresenceNotifier;

    public UpdateUserStatusHandler(
        IUserRepository userRepository,
        IGuildMemberRepository guildMemberRepository,
        IUserPresenceNotifier userPresenceNotifier)
    {
        _userRepository = userRepository;
        _guildMemberRepository = guildMemberRepository;
        _userPresenceNotifier = userPresenceNotifier;
    }

    public async Task<ApplicationResponse<UpdateUserStatusResponse>> HandleAsync(
        UpdateUserStatusRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return ApplicationResponse<UpdateUserStatusResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "User was not found");
        }

        var result = user.UpdateStatus(request.Status);
        if (result.IsFailure)
        {
            return ApplicationResponse<UpdateUserStatusResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.Status),
                    ApplicationErrorCodes.Validation.Invalid,
                    result.Error ?? "Status is invalid"));
        }

        await _userRepository.UpdateStatusAsync(
            user.Id,
            user.Status,
            user.StatusUpdatedAtUtc!.Value,
            cancellationToken);

        // Broadcast to guild members: invisible users appear as "offline" to others
        var broadcastStatus = string.Equals(user.Status, "invisible", StringComparison.OrdinalIgnoreCase)
            ? "offline"
            : user.Status;

        var memberships = await _guildMemberRepository.GetUserGuildMembershipsAsync(
            currentUserId,
            cancellationToken);

        var guildIds = memberships.Select(m => m.Guild.Id).ToList();

        if (guildIds.Count > 0)
        {
            await _userPresenceNotifier.NotifyStatusChangedAsync(
                new UserPresenceChangedNotification(
                    UserId: currentUserId,
                    Status: broadcastStatus,
                    GuildIds: guildIds),
                cancellationToken);
        }

        var payload = new UpdateUserStatusResponse(
            UserId: user.Id.Value,
            Status: user.Status);

        return ApplicationResponse<UpdateUserStatusResponse>.Ok(payload);
    }
}
