using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed class UpdateMemberRoleHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ILogger<UpdateMemberRoleHandler> _logger;

    public UpdateMemberRoleHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        ILogger<UpdateMemberRoleHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        UserId targetId,
        GuildRole newRole,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UpdateMemberRole started. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}, NewRole={NewRole}",
            guildId,
            callerId,
            targetId,
            newRole);

        var guild = await _guildRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
        {
            _logger.LogWarning(
                "UpdateMemberRole failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var callerRole = await _guildMemberRepository.GetRoleAsync(guildId, callerId, cancellationToken);
        if (callerRole is null || callerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "UpdateMemberRole failed because caller is not an admin. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to change member roles in this guild");
        }

        var targetRole = await _guildMemberRepository.GetRoleAsync(guildId, targetId, cancellationToken);
        if (targetRole is null)
        {
            _logger.LogWarning(
                "UpdateMemberRole failed because target is not a member. GuildId={GuildId}, TargetId={TargetId}",
                guildId,
                targetId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        if (guild.OwnerUserId == targetId)
        {
            _logger.LogWarning(
                "UpdateMemberRole failed because target is the guild owner. GuildId={GuildId}, TargetId={TargetId}",
                guildId,
                targetId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged,
                "The guild owner's role cannot be changed");
        }

        await _guildMemberRepository.UpdateRoleAsync(guildId, targetId, newRole, cancellationToken);

        _logger.LogInformation(
            "UpdateMemberRole succeeded. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}, NewRole={NewRole}",
            guildId,
            callerId,
            targetId,
            newRole);

        return ApplicationResponse<bool>.Ok(true);
    }
}
