using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.LeaveGuild;

public sealed class LeaveGuildHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly ILogger<LeaveGuildHandler> _logger;

    public LeaveGuildHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IRealtimeGroupManager realtimeGroupManager,
        ILogger<LeaveGuildHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "LeaveGuild started. GuildId={GuildId}, UserId={UserId}",
            guildId,
            userId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, userId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "LeaveGuild failed because guild was not found. GuildId={GuildId}, UserId={UserId}",
                guildId,
                userId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "LeaveGuild failed because user is not a member. GuildId={GuildId}, UserId={UserId}",
                guildId,
                userId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You are not a member of this guild");
        }

        if (ctx.Guild.OwnerUserId == userId)
        {
            _logger.LogWarning(
                "LeaveGuild failed because user is the guild owner. GuildId={GuildId}, UserId={UserId}",
                guildId,
                userId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerCannotLeave,
                "The guild owner cannot leave the guild");
        }

        await _guildMemberRepository.RemoveAsync(guildId, userId, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.RemoveUserFromGuildGroupsAsync(userId, guildId, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to unsubscribe user {UserId} from guild {GuildId} SignalR groups",
            userId,
            guildId);

        _logger.LogInformation(
            "LeaveGuild succeeded. GuildId={GuildId}, UserId={UserId}",
            guildId,
            userId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
