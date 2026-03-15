using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.ListBans;

public sealed class ListBansHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildBanRepository _guildBanRepository;
    private readonly ILogger<ListBansHandler> _logger;

    public ListBansHandler(
        IGuildRepository guildRepository,
        IGuildBanRepository guildBanRepository,
        ILogger<ListBansHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildBanRepository = guildBanRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<ListBansResponse>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ListBans started. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "ListBans failed because guild was not found. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<ListBansResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "ListBans forbidden. GuildId={GuildId}, CallerId={CallerId}, CallerRole={CallerRole}",
                guildId,
                callerId,
                ctx.CallerRole);

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

        _logger.LogInformation(
            "ListBans succeeded. GuildId={GuildId}, CallerId={CallerId}, BanCount={BanCount}",
            guildId,
            callerId,
            items.Length);

        return ApplicationResponse<ListBansResponse>.Ok(
            new ListBansResponse(GuildId: guildId.ToString(), Bans: items));
    }
}
