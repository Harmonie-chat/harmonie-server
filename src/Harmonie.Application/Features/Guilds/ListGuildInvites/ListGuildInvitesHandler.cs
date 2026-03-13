using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.ListGuildInvites;

public sealed class ListGuildInvitesHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildInviteRepository _guildInviteRepository;
    private readonly ILogger<ListGuildInvitesHandler> _logger;

    public ListGuildInvitesHandler(
        IGuildRepository guildRepository,
        IGuildInviteRepository guildInviteRepository,
        ILogger<ListGuildInvitesHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildInviteRepository = guildInviteRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<ListGuildInvitesResponse>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ListGuildInvites started. GuildId={GuildId}, CallerId={CallerId}",
            guildId,
            callerId);

        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (guildAccess is null)
        {
            _logger.LogWarning(
                "ListGuildInvites failed because guild was not found. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<ListGuildInvitesResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildAccess.CallerRole is null || guildAccess.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "ListGuildInvites forbidden. GuildId={GuildId}, CallerId={CallerId}, CallerRole={CallerRole}",
                guildId,
                callerId,
                guildAccess.CallerRole);

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
            IsExpired: (i.ExpiresAtUtc.HasValue && i.ExpiresAtUtc.Value <= now)
                    || (i.MaxUses.HasValue && i.UsesCount >= i.MaxUses.Value)))
            .ToArray();

        _logger.LogInformation(
            "ListGuildInvites succeeded. GuildId={GuildId}, CallerId={CallerId}, InviteCount={InviteCount}",
            guildId,
            callerId,
            items.Length);

        return ApplicationResponse<ListGuildInvitesResponse>.Ok(
            new ListGuildInvitesResponse(GuildId: guildId.ToString(), Invites: items));
    }
}
