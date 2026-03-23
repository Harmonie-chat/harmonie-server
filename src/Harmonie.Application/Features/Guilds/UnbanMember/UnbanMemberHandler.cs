using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.UnbanMember;

public sealed record UnbanMemberInput(GuildId GuildId, UserId TargetId);

public sealed class UnbanMemberHandler : IAuthenticatedHandler<UnbanMemberInput, bool>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildBanRepository _guildBanRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UnbanMemberHandler(
        IGuildRepository guildRepository,
        IGuildBanRepository guildBanRepository,
        IUnitOfWork unitOfWork)
    {
        _guildRepository = guildRepository;
        _guildBanRepository = guildBanRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        UnbanMemberInput request,
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
                "You must be an admin to unban members from this guild");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        var deleted = await _guildBanRepository.DeleteAsync(request.GuildId, request.TargetId, cancellationToken);
        if (!deleted)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotBanned,
                "User is not banned from this guild");
        }

        await transaction.CommitAsync(cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
