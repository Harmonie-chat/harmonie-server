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
    private readonly ILogger<InviteMemberHandler> _logger;

    public InviteMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        ILogger<InviteMemberHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<InviteMemberResponse>> HandleAsync(
        GuildId guildId,
        InviteMemberRequest request,
        UserId inviterUserId,
        CancellationToken cancellationToken = default)
    {
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

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.UserId),
                    ApplicationErrorCodes.Validation.InvalidFormat,
                    "User ID must be a valid non-empty GUID"));
        }

        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(
            guildId,
            inviterUserId,
            cancellationToken);
        if (guildAccess is null)
        {
            _logger.LogWarning(
                "InviteMember failed because guild was not found. GuildId={GuildId}, InviterUserId={InviterUserId}",
                guildId,
                inviterUserId);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildAccess.CallerRole is null || guildAccess.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "InviteMember forbidden. GuildId={GuildId}, InviterUserId={InviterUserId}, InviterRole={InviterRole}",
                guildId,
                inviterUserId,
                guildAccess.CallerRole);

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteForbidden,
                "Only guild administrators can invite members");
        }

        var targetLookup = await _guildMemberRepository.GetInviteMemberTargetLookupAsync(
            guildId,
            invitedUserId,
            cancellationToken);
        if (!targetLookup.UserExists)
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

        if (targetLookup.IsMember)
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
