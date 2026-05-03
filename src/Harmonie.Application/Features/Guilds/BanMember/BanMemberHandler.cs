using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.BanMember;

public sealed record BanMemberInput(GuildId GuildId, UserId TargetId, string? Reason, int PurgeMessagesDays);

public sealed class BanMemberHandler : IAuthenticatedHandler<BanMemberInput, BanMemberResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildBanRepository _guildBanRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IGuildNotifier _guildNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BanMemberHandler> _logger;

    public BanMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildBanRepository guildBanRepository,
        IMessageRepository messageRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IGuildNotifier guildNotifier,
        IUnitOfWork unitOfWork,
        ILogger<BanMemberHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildBanRepository = guildBanRepository;
        _messageRepository = messageRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _guildNotifier = guildNotifier;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<BanMemberResponse>> HandleAsync(
        BanMemberInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to ban members from this guild");
        }

        if (currentUserId == request.TargetId)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.CannotBanSelf,
                "You cannot ban yourself");
        }

        if (ctx.Guild.OwnerUserId == request.TargetId)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.OwnerCannotBeBanned,
                "The guild owner cannot be banned");
        }

        var targetInfo = await _guildMemberRepository.GetUserWithRoleAsync(request.GuildId, request.TargetId, cancellationToken);
        var isMember = targetInfo is not null;

        if (targetInfo?.Role == GuildRole.Admin && ctx.Guild.OwnerUserId != currentUserId)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only the guild owner can ban an admin");
        }

        var banResult = GuildBan.Create(request.GuildId, request.TargetId, request.Reason, currentUserId);
        if (banResult.IsFailure || banResult.Value is null)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                banResult.Error ?? "Unable to create guild ban");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        var added = await _guildBanRepository.TryAddAsync(banResult.Value, cancellationToken);
        if (!added)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.AlreadyBanned,
                "User is already banned from this guild");
        }

        if (isMember)
            await _guildMemberRepository.RemoveAsync(request.GuildId, request.TargetId, cancellationToken);

        if (request.PurgeMessagesDays > 0)
            await _messageRepository.SoftDeleteByAuthorInGuildAsync(request.GuildId, request.TargetId, request.PurgeMessagesDays, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (isMember)
        {
            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _realtimeGroupManager.RemoveUserFromGuildGroupsAsync(request.TargetId, request.GuildId, ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to unsubscribe banned user {UserId} from guild {GuildId} SignalR groups",
                request.TargetId,
                request.GuildId);

            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _guildNotifier.NotifyMemberBannedAsync(
                    new MemberBannedNotification(
                        GuildId: request.GuildId,
                        GuildName: ctx.Guild.Name.Value,
                        BannedUserId: request.TargetId,
                        Username: targetInfo!.Username,
                        DisplayName: targetInfo.DisplayName),
                    ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to notify guild {GuildId} that user {UserId} was banned",
                request.GuildId,
                request.TargetId);
        }

        var ban = banResult.Value;
        var payload = new BanMemberResponse(
            GuildId: ban.GuildId.Value,
            UserId: ban.UserId.Value,
            Reason: ban.Reason,
            BannedBy: ban.BannedBy.Value,
            CreatedAtUtc: ban.CreatedAtUtc);

        return ApplicationResponse<BanMemberResponse>.Ok(payload);
    }
}
