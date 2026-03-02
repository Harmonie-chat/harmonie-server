using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.TransferOwnership;

public sealed class TransferOwnershipHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferOwnershipHandler> _logger;

    public TransferOwnershipHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IUnitOfWork unitOfWork,
        ILogger<TransferOwnershipHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        UserId newOwnerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "TransferOwnership started. GuildId={GuildId}, CallerId={CallerId}, NewOwnerId={NewOwnerId}",
            guildId,
            callerId,
            newOwnerId);

        if (newOwnerId == callerId)
        {
            _logger.LogWarning(
                "TransferOwnership failed because caller tried to transfer ownership to themselves. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerTransferToSelf,
                "Cannot transfer ownership to yourself");
        }

        // Begin the transaction before the membership check so that
        // UpdateRoleAsync executes in the same transaction scope.
        // UpdateRoleAsync returns the number of rows affected: if 0, the
        // member was removed concurrently after the check passed, and the
        // transaction is abandoned (not committed) to keep state consistent.
        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        // Fetch guild and new owner's membership in a single query.
        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, newOwnerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "TransferOwnership failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.Guild.OwnerUserId != callerId)
        {
            _logger.LogWarning(
                "TransferOwnership failed because caller is not the guild owner. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner can transfer ownership");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "TransferOwnership failed because new owner is not a member. GuildId={GuildId}, NewOwnerId={NewOwnerId}",
                guildId,
                newOwnerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        await _guildRepository.UpdateOwnerAsync(guildId, newOwnerId, cancellationToken);
        var rowsUpdated = await _guildMemberRepository.UpdateRoleAsync(guildId, newOwnerId, GuildRole.Admin, cancellationToken);

        if (rowsUpdated == 0)
        {
            _logger.LogWarning(
                "TransferOwnership aborted: new owner membership was deleted concurrently. GuildId={GuildId}, NewOwnerId={NewOwnerId}",
                guildId,
                newOwnerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "TransferOwnership succeeded. GuildId={GuildId}, PreviousOwnerId={PreviousOwnerId}, NewOwnerId={NewOwnerId}",
            guildId,
            callerId,
            newOwnerId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
