using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.TransferOwnership;

public sealed record TransferOwnershipInput(GuildId GuildId, UserId NewOwnerId);

public sealed class TransferOwnershipHandler
    : IAuthenticatedHandler<TransferOwnershipInput, bool>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TransferOwnershipHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        TransferOwnershipInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.NewOwnerId == currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerTransferToSelf,
                "Cannot transfer ownership to yourself");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, request.NewOwnerId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.Guild.OwnerUserId != currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner can transfer ownership");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        await _guildRepository.UpdateOwnerAsync(request.GuildId, request.NewOwnerId, cancellationToken);
        var rowsUpdated = await _guildMemberRepository.UpdateRoleAsync(request.GuildId, request.NewOwnerId, GuildRole.Admin, cancellationToken);

        if (rowsUpdated == 0)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        await transaction.CommitAsync(cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
