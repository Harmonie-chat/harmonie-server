using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.RemoveMember;

public sealed record RemoveMemberInput(GuildId GuildId, UserId TargetId);

public sealed class RemoveMemberHandler : IAuthenticatedHandler<RemoveMemberInput, bool>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IGuildNotifier _guildNotifier;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<RemoveMemberHandler> _logger;

    public RemoveMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IGuildNotifier guildNotifier,
        IUserRepository userRepository,
        ILogger<RemoveMemberHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _guildNotifier = guildNotifier;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        RemoveMemberInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to remove members from this guild");
        }

        var targetRole = await _guildMemberRepository.GetRoleAsync(request.GuildId, request.TargetId, cancellationToken);
        if (targetRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        if (ctx.Guild.OwnerUserId == request.TargetId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerCannotBeRemoved,
                "The guild owner cannot be removed from the guild");
        }

        await _guildMemberRepository.RemoveAsync(request.GuildId, request.TargetId, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.RemoveUserFromGuildGroupsAsync(request.TargetId, request.GuildId, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to unsubscribe user {UserId} from guild {GuildId} SignalR groups",
            request.TargetId,
            request.GuildId);

        var removedUser = await _userRepository.GetByIdAsync(request.TargetId, CancellationToken.None);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _guildNotifier.NotifyMemberRemovedAsync(
                new MemberRemovedNotification(
                    GuildId: request.GuildId,
                    RemovedUserId: request.TargetId,
                    Username: removedUser?.Username.Value ?? string.Empty,
                    DisplayName: removedUser?.DisplayName),
                ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to notify guild {GuildId} that user {UserId} was removed",
            request.GuildId,
            request.TargetId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
