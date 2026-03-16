using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.BanMember;

public sealed class BanMemberHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildBanRepository _guildBanRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BanMemberHandler> _logger;

    public BanMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildBanRepository guildBanRepository,
        IMessageRepository messageRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IUnitOfWork unitOfWork,
        ILogger<BanMemberHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildBanRepository = guildBanRepository;
        _messageRepository = messageRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<BanMemberResponse>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        UserId targetId,
        string? reason,
        int purgeMessagesDays,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "BanMember started. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}",
            guildId,
            callerId,
            targetId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "BanMember failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "BanMember failed because caller is not an admin. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to ban members from this guild");
        }

        if (callerId == targetId)
        {
            _logger.LogWarning(
                "BanMember failed because caller tried to ban themselves. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.CannotBanSelf,
                "You cannot ban yourself");
        }

        if (ctx.Guild.OwnerUserId == targetId)
        {
            _logger.LogWarning(
                "BanMember failed because target is the guild owner. GuildId={GuildId}, TargetId={TargetId}",
                guildId,
                targetId);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.OwnerCannotBeBanned,
                "The guild owner cannot be banned");
        }

        var targetRole = await _guildMemberRepository.GetRoleAsync(guildId, targetId, cancellationToken);
        var isMember = targetRole is not null;

        if (targetRole == GuildRole.Admin && ctx.Guild.OwnerUserId != callerId)
        {
            _logger.LogWarning(
                "BanMember failed because non-owner admin tried to ban another admin. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}",
                guildId,
                callerId,
                targetId);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner can ban an admin");
        }

        var banResult = GuildBan.Create(guildId, targetId, reason, callerId);
        if (banResult.IsFailure || banResult.Value is null)
        {
            _logger.LogWarning(
                "BanMember ban creation failed. GuildId={GuildId}, Error={Error}",
                guildId,
                banResult.Error);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                banResult.Error ?? "Unable to create guild ban");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        var added = await _guildBanRepository.TryAddAsync(banResult.Value, cancellationToken);
        if (!added)
        {
            _logger.LogWarning(
                "BanMember failed because user is already banned. GuildId={GuildId}, TargetId={TargetId}",
                guildId,
                targetId);

            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.AlreadyBanned,
                "User is already banned from this guild");
        }

        if (isMember)
            await _guildMemberRepository.RemoveAsync(guildId, targetId, cancellationToken);

        if (purgeMessagesDays > 0)
            await _messageRepository.SoftDeleteByAuthorInGuildAsync(guildId, targetId, purgeMessagesDays, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (isMember)
        {
            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _realtimeGroupManager.RemoveUserFromGuildGroupsAsync(targetId, guildId, ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to unsubscribe banned user {UserId} from guild {GuildId} SignalR groups",
                targetId,
                guildId);
        }

        _logger.LogInformation(
            "BanMember succeeded. GuildId={GuildId}, CallerId={CallerId}, TargetId={TargetId}, IsMember={IsMember}, PurgeDays={PurgeDays}",
            guildId,
            callerId,
            targetId,
            isMember,
            purgeMessagesDays);

        var ban = banResult.Value;
        var payload = new BanMemberResponse(
            GuildId: ban.GuildId.ToString(),
            UserId: ban.UserId.ToString(),
            Reason: ban.Reason,
            BannedBy: ban.BannedBy.ToString(),
            CreatedAtUtc: ban.CreatedAtUtc);

        return ApplicationResponse<BanMemberResponse>.Ok(payload);
    }
}
