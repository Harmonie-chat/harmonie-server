using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Guilds.InviteMember;

public sealed class InviteMemberHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IUserRepository _userRepository;

    public InviteMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IUserRepository userRepository)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _userRepository = userRepository;
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

        if (!UserId.TryParse(request.UserId, out var invitedUserId) || invitedUserId is null)
        {
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
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");

        var inviterRole = await _guildMemberRepository.GetRoleAsync(
            guildId,
            inviterUserId,
            cancellationToken);
        if (inviterRole is null || inviterRole != GuildRole.Admin)
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteForbidden,
                "Only guild administrators can invite members");

        var invitedUser = await _userRepository.GetByIdAsync(invitedUserId, cancellationToken);
        if (invitedUser is null)
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteTargetNotFound,
                "Invite target user was not found");

        var alreadyMember = await _guildMemberRepository.IsMemberAsync(
            guildId,
            invitedUserId,
            cancellationToken);
        if (alreadyMember)
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "Target user is already a guild member");

        var memberResult = GuildMember.Create(
            guildId,
            invitedUserId,
            GuildRole.Member,
            invitedByUserId: inviterUserId);
        if (memberResult.IsFailure || memberResult.Value is null)
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                memberResult.Error ?? "Unable to create guild membership");

        var added = await _guildMemberRepository.TryAddAsync(memberResult.Value, cancellationToken);
        if (!added)
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "Target user is already a guild member");

        var payload = new InviteMemberResponse(
            GuildId: guildId.ToString(),
            UserId: invitedUserId.ToString(),
            Role: GuildRole.Member.ToString(),
            JoinedAtUtc: memberResult.Value.JoinedAtUtc);

        return ApplicationResponse<InviteMemberResponse>.Ok(payload);
    }
}
