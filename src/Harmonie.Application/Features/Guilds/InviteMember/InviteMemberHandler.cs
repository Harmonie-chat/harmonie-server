using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.InviteMember;

public sealed class InviteMemberHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<InviteMemberHandler> _logger;

    public InviteMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IUserRepository userRepository,
        ILogger<InviteMemberHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<InviteMemberResponse>> HandleAsync(
        GuildId guildId,
        InviteMemberRequest request,
        UserId inviterUserId,
        CancellationToken cancellationToken = default)
    {
        if (guildId is null)
            throw new ArgumentNullException(nameof(guildId));
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (inviterUserId is null)
            throw new ArgumentNullException(nameof(inviterUserId));

        _logger.LogInformation(
            "InviteMember started. GuildId={GuildId}, InviterUserId={InviterUserId}, TargetUserIdRaw={TargetUserIdRaw}",
            guildId,
            inviterUserId,
            request.UserId);

        if (!UserId.TryParse(request.UserId, out var invitedUserId) || invitedUserId is null)
        {
            _logger.LogWarning(
                "InviteMember validation failed. GuildId={GuildId}, InviterUserId={InviterUserId}, TargetUserIdRaw={TargetUserIdRaw}",
                guildId,
                inviterUserId,
                request.UserId);

            var details = new Dictionary<string, string[]>
            {
                [nameof(request.UserId)] = ["User ID must be a valid non-empty GUID"]
            };

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details);
        }

        var guild = await _guildRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
        {
            _logger.LogWarning(
                "InviteMember failed because guild was not found. GuildId={GuildId}, InviterUserId={InviterUserId}",
                guildId,
                inviterUserId);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var inviterRole = await _guildMemberRepository.GetRoleAsync(
            guildId,
            inviterUserId,
            cancellationToken);
        if (inviterRole is null || inviterRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "InviteMember forbidden. GuildId={GuildId}, InviterUserId={InviterUserId}, InviterRole={InviterRole}",
                guildId,
                inviterUserId,
                inviterRole);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteForbidden,
                "Only guild administrators can invite members");
        }

        var invitedUser = await _userRepository.GetByIdAsync(invitedUserId, cancellationToken);
        if (invitedUser is null)
        {
            _logger.LogWarning(
                "InviteMember target user not found. GuildId={GuildId}, InviterUserId={InviterUserId}, TargetUserId={TargetUserId}",
                guildId,
                inviterUserId,
                invitedUserId);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteTargetNotFound,
                "Invite target user was not found");
        }

        var alreadyMember = await _guildMemberRepository.IsMemberAsync(
            guildId,
            invitedUserId,
            cancellationToken);
        if (alreadyMember)
        {
            _logger.LogWarning(
                "InviteMember target already member. GuildId={GuildId}, TargetUserId={TargetUserId}",
                guildId,
                invitedUserId);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "Target user is already a guild member");
        }

        var memberResult = GuildMember.Create(
            guildId,
            invitedUserId,
            GuildRole.Member,
            invitedByUserId: inviterUserId);
        if (memberResult.IsFailure || memberResult.Value is null)
        {
            _logger.LogWarning(
                "InviteMember member creation failed. GuildId={GuildId}, TargetUserId={TargetUserId}, Error={Error}",
                guildId,
                invitedUserId,
                memberResult.Error);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                memberResult.Error ?? "Unable to create guild membership");
        }

        var added = await _guildMemberRepository.TryAddAsync(memberResult.Value, cancellationToken);
        if (!added)
        {
            _logger.LogWarning(
                "InviteMember member insert conflict. GuildId={GuildId}, TargetUserId={TargetUserId}",
                guildId,
                invitedUserId);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "Target user is already a guild member");
        }

        _logger.LogInformation(
            "InviteMember succeeded. GuildId={GuildId}, InviterUserId={InviterUserId}, TargetUserId={TargetUserId}",
            guildId,
            inviterUserId,
            invitedUserId);

        var payload = new InviteMemberResponse(
            GuildId: guildId.ToString(),
            UserId: invitedUserId.ToString(),
            Role: GuildRole.Member.ToString(),
            JoinedAtUtc: memberResult.Value.JoinedAtUtc);

        return ApplicationResponse<InviteMemberResponse>.Ok(payload);
    }
}
