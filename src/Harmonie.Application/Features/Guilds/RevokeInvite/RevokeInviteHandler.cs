using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.RevokeInvite;

public sealed record RevokeInviteInput(GuildId GuildId, string InviteCode);

public sealed class RevokeInviteHandler : IAuthenticatedHandler<RevokeInviteInput, bool>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeInviteHandler(
        IGuildRepository guildRepository,
        IGuildInviteRepository guildInviteRepository,
        IUnitOfWork unitOfWork)
    {
        _guildRepository = guildRepository;
        _guildInviteRepository = guildInviteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        RevokeInviteInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var invite = await _guildInviteRepository.GetRevokeDetailsByCodeAsync(request.GuildId, request.InviteCode, cancellationToken);
        if (invite is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Invite.NotFound,
                "Invite was not found");
        }

        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, currentUserId, cancellationToken);
        var callerRole = guildAccess?.CallerRole;

        var isAdmin = callerRole == GuildRole.Admin;
        var isCreator = invite.CreatorId == currentUserId;

        if (!isAdmin && !isCreator)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Invite.RevokeForbidden,
                "Only guild administrators or the invite creator can revoke this invite");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildInviteRepository.RevokeAsync(request.InviteCode, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
