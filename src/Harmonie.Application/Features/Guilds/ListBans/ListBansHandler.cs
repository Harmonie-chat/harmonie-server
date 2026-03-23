using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.ListBans;

public sealed class ListBansHandler : IAuthenticatedHandler<GuildId, ListBansResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildBanRepository _guildBanRepository;

    public ListBansHandler(
        IGuildRepository guildRepository,
        IGuildBanRepository guildBanRepository)
    {
        _guildRepository = guildRepository;
        _guildBanRepository = guildBanRepository;
    }

    public async Task<ApplicationResponse<ListBansResponse>> HandleAsync(
        GuildId guildId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<ListBansResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<ListBansResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You must be an admin to list bans in this guild");
        }

        var bans = await _guildBanRepository.GetByGuildIdAsync(guildId, cancellationToken);

        var items = bans.Select(b =>
        {
            var avatar = b.AvatarColor is not null || b.AvatarIcon is not null || b.AvatarBg is not null
                ? new AvatarAppearanceDto(b.AvatarColor, b.AvatarIcon, b.AvatarBg)
                : null;

            return new ListBansItemResponse(
                UserId: b.UserId.ToString(),
                Username: b.Username.Value,
                DisplayName: b.DisplayName,
                AvatarFileId: b.AvatarFileId?.ToString(),
                Avatar: avatar,
                Reason: b.Reason,
                BannedBy: b.BannedBy.ToString(),
                CreatedAtUtc: b.CreatedAtUtc);
        }).ToArray();

        return ApplicationResponse<ListBansResponse>.Ok(
            new ListBansResponse(GuildId: guildId.ToString(), Bans: items));
    }
}
