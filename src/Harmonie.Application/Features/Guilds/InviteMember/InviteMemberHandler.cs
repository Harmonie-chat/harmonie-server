using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.InviteMember;

public sealed record InviteMemberInput(GuildId GuildId, InviteMemberRequest Request);

public sealed class InviteMemberHandler : IAuthenticatedHandler<InviteMemberInput, InviteMemberResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;

    public InviteMemberHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
    }

    public async Task<ApplicationResponse<InviteMemberResponse>> HandleAsync(
        InviteMemberInput input,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var (guildId, request) = input;

        if (!UserId.TryParse(request.UserId, out var invitedUserId) || invitedUserId is null)
        {
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
            currentUserId,
            cancellationToken);
        if (guildAccess is null)
        {
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildAccess.CallerRole is null || guildAccess.CallerRole != GuildRole.Admin)
        {
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
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteTargetNotFound,
                "Invite target user was not found");
        }

        if (targetLookup.IsMember)
        {
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "Target user is already a guild member");
        }

        var memberResult = GuildMember.Create(
            guildId,
            invitedUserId,
            GuildRole.Member,
            invitedByUserId: currentUserId);
        if (memberResult.IsFailure || memberResult.Value is null)
        {
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                memberResult.Error ?? "Unable to create guild membership");
        }

        var added = await _guildMemberRepository.TryAddAsync(memberResult.Value, cancellationToken);
        if (!added)
        {
            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Guild.MemberAlreadyExists,
                "Target user is already a guild member");
        }

        var payload = new InviteMemberResponse(
            GuildId: guildId.ToString(),
            UserId: invitedUserId.ToString(),
            Role: GuildRole.Member.ToString(),
            JoinedAtUtc: memberResult.Value.JoinedAtUtc);

        return ApplicationResponse<InviteMemberResponse>.Ok(payload);
    }
}
