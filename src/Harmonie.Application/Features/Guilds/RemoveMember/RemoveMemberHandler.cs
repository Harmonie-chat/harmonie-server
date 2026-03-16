using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.RemoveMember;

public sealed class RemoveMemberHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly ILogger<RemoveMemberHandler> _logger;

    public RemoveMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IRealtimeGroupManager realtimeGroupManager,
        ILogger<RemoveMemberHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        UserId targetId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "RemoveMember started. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}",
            guildId,
            callerId,
            targetId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "RemoveMember failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "RemoveMember failed because caller is not an admin. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to remove members from this guild");
        }

        var targetRole = await _guildMemberRepository.GetRoleAsync(guildId, targetId, cancellationToken);
        if (targetRole is null)
        {
            _logger.LogWarning(
                "RemoveMember failed because target is not a member. GuildId={GuildId}, TargetId={TargetId}",
                guildId,
                targetId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        if (ctx.Guild.OwnerUserId == targetId)
        {
            _logger.LogWarning(
                "RemoveMember failed because target is the guild owner. GuildId={GuildId}, TargetId={TargetId}",
                guildId,
                targetId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerCannotBeRemoved,
                "The guild owner cannot be removed from the guild");
        }

        await _guildMemberRepository.RemoveAsync(guildId, targetId, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.RemoveUserFromGuildGroupsAsync(targetId, guildId, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to unsubscribe user {UserId} from guild {GuildId} SignalR groups",
            targetId,
            guildId);

        _logger.LogInformation(
            "RemoveMember succeeded. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}",
            guildId,
            callerId,
            targetId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
