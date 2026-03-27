using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public sealed class GetGuildMembersHandler : IAuthenticatedHandler<GuildId, GetGuildMembersResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;

    public GetGuildMembersHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
    }

    public async Task<ApplicationResponse<GetGuildMembersResponse>> HandleAsync(
        GuildId guildId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<GetGuildMembersResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<GetGuildMembersResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        var members = await _guildMemberRepository.GetGuildMembersAsync(guildId, cancellationToken);

        var payload = new GetGuildMembersResponse(
            GuildId: guildId.Value,
            Members: members.Select(member =>
                {
                    var avatar = member.AvatarColor is not null || member.AvatarIcon is not null || member.AvatarBg is not null
                        ? new AvatarAppearanceDto(member.AvatarColor, member.AvatarIcon, member.AvatarBg)
                        : null;

                    return new GetGuildMembersItemResponse(
                        UserId: member.UserId.Value,
                        Username: member.Username.Value,
                        DisplayName: member.DisplayName,
                        AvatarFileId: member.AvatarFileId?.Value,
                        Avatar: avatar,
                        Bio: member.Bio,
                        IsActive: member.IsActive,
                        Role: member.Role.ToString(),
                        JoinedAtUtc: member.JoinedAtUtc);
                })
                .ToArray());

        return ApplicationResponse<GetGuildMembersResponse>.Ok(payload);
    }
}
