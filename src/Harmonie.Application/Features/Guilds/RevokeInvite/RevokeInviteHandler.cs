using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.RevokeInvite;

public sealed class RevokeInviteHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevokeInviteHandler> _logger;

    public RevokeInviteHandler(
        IGuildRepository guildRepository,
        IGuildInviteRepository guildInviteRepository,
        IUnitOfWork unitOfWork,
        ILogger<RevokeInviteHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildInviteRepository = guildInviteRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildId guildId,
        string inviteCode,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "RevokeInvite started. GuildId={GuildId}, InviteCode={InviteCode}, CallerId={CallerId}",
            guildId,
            inviteCode,
            callerId);

        var invite = await _guildInviteRepository.GetRevokeDetailsByCodeAsync(guildId, inviteCode, cancellationToken);
        if (invite is null)
        {
            _logger.LogWarning(
                "RevokeInvite failed because invite was not found. GuildId={GuildId}, InviteCode={InviteCode}",
                guildId,
                inviteCode);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Invite.NotFound,
                "Invite was not found");
        }

        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        var callerRole = guildAccess?.CallerRole;

        var isAdmin = callerRole == GuildRole.Admin;
        var isCreator = invite.CreatorId == callerId;

        if (!isAdmin && !isCreator)
        {
            _logger.LogWarning(
                "RevokeInvite forbidden. GuildId={GuildId}, InviteCode={InviteCode}, CallerId={CallerId}, CallerRole={CallerRole}",
                guildId,
                inviteCode,
                callerId,
                callerRole);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Invite.RevokeForbidden,
                "Only guild administrators or the invite creator can revoke this invite");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildInviteRepository.RevokeAsync(inviteCode, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "RevokeInvite succeeded. GuildId={GuildId}, InviteCode={InviteCode}, CallerId={CallerId}",
            guildId,
            inviteCode,
            callerId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
