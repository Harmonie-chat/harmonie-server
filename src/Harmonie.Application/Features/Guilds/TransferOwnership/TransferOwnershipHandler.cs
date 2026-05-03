using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.TransferOwnership;

public sealed record TransferOwnershipInput(GuildId GuildId, UserId NewOwnerId);

public sealed class TransferOwnershipHandler
    : IAuthenticatedHandler<TransferOwnershipInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildNotifier _guildNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferOwnershipHandler> _logger;

    public TransferOwnershipHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildNotifier guildNotifier,
        IUnitOfWork unitOfWork,
        ILogger<TransferOwnershipHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildNotifier = guildNotifier;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

        await NotifyOwnershipTransferredSafelyAsync(
            new GuildOwnershipTransferredNotification(
                request.GuildId,
                ctx.Guild.Name.Value,
                request.NewOwnerId,
                ctx.CallerUsername ?? string.Empty,
                ctx.CallerDisplayName));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyOwnershipTransferredSafelyAsync(
        GuildOwnershipTransferredNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _guildNotifier.NotifyGuildOwnershipTransferredAsync(notification, token),
            NotificationTimeout,
            _logger,
            "TransferOwnership notification failed (best-effort). GuildId={GuildId}",
            notification.GuildId);
    }
}
