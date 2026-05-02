using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed record UpdateMemberRoleInput(GuildId GuildId, UserId TargetId, GuildRole NewRole);

public sealed class UpdateMemberRoleHandler
    : IAuthenticatedHandler<UpdateMemberRoleInput, bool>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildNotifier _guildNotifier;

    public UpdateMemberRoleHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildNotifier guildNotifier)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildNotifier = guildNotifier;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        UpdateMemberRoleInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(request.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to change member roles in this guild");
        }

        var targetInfo = await _guildMemberRepository.GetUserWithRoleAsync(request.GuildId, request.TargetId, cancellationToken);
        if (targetInfo is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.MemberNotFound,
                "The specified user is not a member of this guild");
        }

        if (ctx.Guild.OwnerUserId == request.TargetId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged,
                "The guild owner's role cannot be changed");
        }

        await _guildMemberRepository.UpdateRoleAsync(request.GuildId, request.TargetId, request.NewRole, cancellationToken);

        await _guildNotifier.NotifyMemberRoleUpdatedAsync(
            new MemberRoleUpdatedNotification(
                request.GuildId,
                request.TargetId,
                targetInfo!.Username,
                targetInfo.DisplayName,
                request.NewRole),
            cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
