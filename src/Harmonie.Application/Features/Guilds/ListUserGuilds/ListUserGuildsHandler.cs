using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.ListUserGuilds;

public sealed class ListUserGuildsHandler
{
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ILogger<ListUserGuildsHandler> _logger;

    public ListUserGuildsHandler(
        IGuildMemberRepository guildMemberRepository,
        ILogger<ListUserGuildsHandler> logger)
    {
        _guildMemberRepository = guildMemberRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<ListUserGuildsResponse>> HandleAsync(
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ListUserGuilds started for user {UserId}",
            currentUserId);

        var memberships = await _guildMemberRepository.GetUserGuildMembershipsAsync(
            currentUserId,
            cancellationToken);

        var payload = new ListUserGuildsResponse(
            memberships.Select(membership => new ListUserGuildsItemResponse(
                    GuildId: membership.Guild.Id.ToString(),
                    Name: membership.Guild.Name.Value,
                    OwnerUserId: membership.Guild.OwnerUserId.ToString(),
                    IconFileId: membership.Guild.IconFileId?.ToString(),
                    Icon: membership.Guild.IconColor is not null
                        || membership.Guild.IconName is not null
                        || membership.Guild.IconBg is not null
                        ? new GuildIconDto(
                            membership.Guild.IconColor,
                            membership.Guild.IconName,
                            membership.Guild.IconBg)
                        : null,
                    Role: membership.Role.ToString(),
                    JoinedAtUtc: membership.JoinedAtUtc))
                .ToArray());

        _logger.LogInformation(
            "ListUserGuilds succeeded for user {UserId}. GuildCount={GuildCount}",
            currentUserId,
            payload.Guilds.Count);

        return ApplicationResponse<ListUserGuildsResponse>.Ok(payload);
    }
}
