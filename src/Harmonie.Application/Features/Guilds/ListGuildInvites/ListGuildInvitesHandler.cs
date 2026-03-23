using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.ListGuildInvites;

public sealed class ListGuildInvitesHandler : IAuthenticatedHandler<GuildId, ListGuildInvitesResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildInviteRepository _guildInviteRepository;

    public ListGuildInvitesHandler(
        IGuildRepository guildRepository,
        IGuildInviteRepository guildInviteRepository)
    {
        _guildRepository = guildRepository;
        _guildInviteRepository = guildInviteRepository;
    }

    public async Task<ApplicationResponse<ListGuildInvitesResponse>> HandleAsync(
        GuildId guildId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
        if (guildAccess is null)
        {
            return ApplicationResponse<ListGuildInvitesResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildAccess.CallerRole is null || guildAccess.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<ListGuildInvitesResponse>.Fail(
                ApplicationErrorCodes.Guild.InviteForbidden,
                "Only guild administrators can list invite links");
        }

        var invites = await _guildInviteRepository.GetByGuildIdAsync(guildId, cancellationToken);

        var now = DateTime.UtcNow;
        var items = invites.Select(i => new ListGuildInvitesItemResponse(
            Code: i.Code,
            CreatorId: i.CreatorId.ToString(),
            UsesCount: i.UsesCount,
            MaxUses: i.MaxUses,
            ExpiresAtUtc: i.ExpiresAtUtc,
            CreatedAtUtc: i.CreatedAtUtc,
            RevokedAtUtc: i.RevokedAtUtc,
            IsExpired: i.RevokedAtUtc.HasValue
                    || (i.ExpiresAtUtc.HasValue && i.ExpiresAtUtc.Value <= now)
                    || (i.MaxUses.HasValue && i.UsesCount >= i.MaxUses.Value)))
            .ToArray();

        return ApplicationResponse<ListGuildInvitesResponse>.Ok(
            new ListGuildInvitesResponse(GuildId: guildId.ToString(), Invites: items));
    }
}
