using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.AcceptInvite;

public sealed class AcceptInviteHandler : IAuthenticatedHandler<string, AcceptInviteResponse>
{
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildBanRepository _guildBanRepository;
    private readonly IGuildRepository _guildRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IGuildNotifier _guildNotifier;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcceptInviteHandler> _logger;

    public AcceptInviteHandler(
        IGuildInviteRepository guildInviteRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildBanRepository guildBanRepository,
        IGuildRepository guildRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IGuildNotifier guildNotifier,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<AcceptInviteHandler> logger)
    {
        _guildInviteRepository = guildInviteRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildBanRepository = guildBanRepository;
        _guildRepository = guildRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _guildNotifier = guildNotifier;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<AcceptInviteResponse>> HandleAsync(
        string inviteCode,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var invite = await _guildInviteRepository.GetAcceptDetailsByCodeAsync(inviteCode, cancellationToken);
        if (invite is null)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.NotFound,
                "Invite was not found");
        }

        if (invite.ExpiresAtUtc.HasValue && invite.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.Expired,
                "This invite has expired");
        }

        if (invite.MaxUses.HasValue && invite.UsesCount >= invite.MaxUses.Value)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.Exhausted,
                "This invite has reached its maximum number of uses");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            invite.GuildId,
            currentUserId,
            cancellationToken);
        if (isMember)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "You are already a member of this guild");
        }

        var isBanned = await _guildBanRepository.ExistsAsync(invite.GuildId, currentUserId, cancellationToken);
        if (isBanned)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.UserBanned,
                "You are banned from this guild");
        }

        var memberResult = GuildMember.Create(
            invite.GuildId,
            currentUserId,
            GuildRole.Member,
            invitedByUserId: invite.CreatorId);
        if (memberResult.IsFailure || memberResult.Value is null)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                memberResult.Error ?? "Unable to create guild membership");
        }

        var guild = await _guildRepository.GetByIdAsync(invite.GuildId, cancellationToken);
        var guildName = guild?.Name.Value ?? "Unknown Guild";

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        var added = await _guildMemberRepository.TryAddAsync(memberResult.Value, cancellationToken);
        if (!added)
        {
            return ApplicationResponse<AcceptInviteResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "You are already a member of this guild");
        }

        await _guildInviteRepository.IncrementUsesCountAsync(inviteCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.AddUserToGuildGroupsAsync(currentUserId, invite.GuildId, ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to subscribe user {UserId} to guild {GuildId} SignalR groups",
            currentUserId,
            invite.GuildId);

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _guildNotifier.NotifyMemberJoinedAsync(
                new MemberJoinedNotification(
                    GuildId: invite.GuildId,
                    GuildName: guildName,
                    UserId: currentUserId,
                    Username: user?.Username.Value ?? string.Empty,
                    DisplayName: user?.DisplayName,
                    AvatarFileId: user?.AvatarFileId),
                ct),
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to notify guild {GuildId} that user {UserId} joined",
            invite.GuildId,
            currentUserId);

        var payload = new AcceptInviteResponse(
            GuildId: invite.GuildId.Value,
            UserId: currentUserId.Value,
            Role: GuildRole.Member.ToString(),
            JoinedAtUtc: memberResult.Value.JoinedAtUtc);

        return ApplicationResponse<AcceptInviteResponse>.Ok(payload);
    }
}
