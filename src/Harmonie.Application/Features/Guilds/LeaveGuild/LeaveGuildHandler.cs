using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.LeaveGuild;

public sealed record LeaveGuildInput(GuildId GuildId);

public sealed class LeaveGuildHandler : IAuthenticatedHandler<LeaveGuildInput, bool>
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
        LeaveGuildInput request,
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

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You are not a member of this guild");
        }

        if (ctx.Guild.OwnerUserId == currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerCannotLeave,
                "The guild owner cannot leave the guild");
        }

        await _guildMemberRepository.RemoveAsync(request.GuildId, currentUserId, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.RemoveUserFromGuildGroupsAsync(currentUserId, request.GuildId, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to unsubscribe user {UserId} from guild {GuildId} SignalR groups",
            currentUserId,
            request.GuildId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
