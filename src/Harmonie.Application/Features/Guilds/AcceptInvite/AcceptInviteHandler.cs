using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.AcceptInvite;

public sealed class AcceptInviteHandler
{
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildBanRepository _guildBanRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcceptInviteHandler> _logger;

    public AcceptInviteHandler(
        IGuildInviteRepository guildInviteRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildBanRepository guildBanRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IUnitOfWork unitOfWork,
        ILogger<AcceptInviteHandler> logger)
    {
        _guildInviteRepository = guildInviteRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildBanRepository = guildBanRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<AcceptInviteResponse>> HandleAsync(
        string inviteCode,
        UserId callerUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AcceptInvite started. InviteCode={InviteCode}, CallerUserId={CallerUserId}",
            inviteCode,
            callerUserId);

        var invite = await _guildInviteRepository.GetAcceptDetailsByCodeAsync(inviteCode, cancellationToken);
        if (invite is null)
        {
            _logger.LogWarning(
                "AcceptInvite failed because invite was not found. InviteCode={InviteCode}",
                inviteCode);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.NotFound,
                "Invite was not found");
        }

        if (invite.ExpiresAtUtc.HasValue && invite.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "AcceptInvite failed because invite has expired. InviteCode={InviteCode}, ExpiresAtUtc={ExpiresAtUtc}",
                inviteCode,
                invite.ExpiresAtUtc);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.Expired,
                "This invite has expired");
        }

        if (invite.MaxUses.HasValue && invite.UsesCount >= invite.MaxUses.Value)
        {
            _logger.LogWarning(
                "AcceptInvite failed because invite has reached max uses. InviteCode={InviteCode}, UsesCount={UsesCount}, MaxUses={MaxUses}",
                inviteCode,
                invite.UsesCount,
                invite.MaxUses);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.Exhausted,
                "This invite has reached its maximum number of uses");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            invite.GuildId,
            callerUserId,
            cancellationToken);
        if (isMember)
        {
            _logger.LogWarning(
                "AcceptInvite failed because user is already a member. InviteCode={InviteCode}, GuildId={GuildId}, CallerUserId={CallerUserId}",
                inviteCode,
                invite.GuildId,
                callerUserId);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "You are already a member of this guild");
        }

        var isBanned = await _guildBanRepository.ExistsAsync(invite.GuildId, callerUserId, cancellationToken);
        if (isBanned)
        {
            _logger.LogWarning(
                "AcceptInvite failed because user is banned. InviteCode={InviteCode}, GuildId={GuildId}, CallerUserId={CallerUserId}",
                inviteCode,
                invite.GuildId,
                callerUserId);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.UserBanned,
                "You are banned from this guild");
        }

        var memberResult = GuildMember.Create(
            invite.GuildId,
            callerUserId,
            GuildRole.Member,
            invitedByUserId: invite.CreatorId);
        if (memberResult.IsFailure || memberResult.Value is null)
        {
            _logger.LogWarning(
                "AcceptInvite member creation failed. GuildId={GuildId}, CallerUserId={CallerUserId}, Error={Error}",
                invite.GuildId,
                callerUserId,
                memberResult.Error);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                memberResult.Error ?? "Unable to create guild membership");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        var added = await _guildMemberRepository.TryAddAsync(memberResult.Value, cancellationToken);
        if (!added)
        {
            _logger.LogWarning(
                "AcceptInvite member insert conflict. GuildId={GuildId}, CallerUserId={CallerUserId}",
                invite.GuildId,
                callerUserId);

            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "You are already a member of this guild");
        }

        await _guildInviteRepository.IncrementUsesCountAsync(inviteCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "AcceptInvite succeeded. InviteCode={InviteCode}, GuildId={GuildId}, CallerUserId={CallerUserId}",
            inviteCode,
            invite.GuildId,
            callerUserId);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.AddUserToGuildGroupsAsync(callerUserId, invite.GuildId, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to subscribe user {UserId} to guild {GuildId} SignalR groups",
            callerUserId,
            invite.GuildId);

        var payload = new AcceptInviteResponse(
            GuildId: invite.GuildId.ToString(),
            UserId: callerUserId.ToString(),
            Role: GuildRole.Member.ToString(),
            JoinedAtUtc: memberResult.Value.JoinedAtUtc);

        return ApplicationResponse<AcceptInviteResponse>.Ok(payload);
    }
}
