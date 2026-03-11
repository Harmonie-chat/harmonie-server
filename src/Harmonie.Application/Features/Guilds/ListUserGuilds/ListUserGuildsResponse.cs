using Harmonie.Application.Features.Guilds;

namespace Harmonie.Application.Features.Guilds.ListUserGuilds;

public sealed record ListUserGuildsResponse(
    IReadOnlyList<ListUserGuildsItemResponse> Guilds);

public sealed record ListUserGuildsItemResponse(
    string GuildId,
    string Name,
    string OwnerUserId,
    string? IconFileId,
    GuildIconDto? Icon,
    string Role,
    DateTime JoinedAtUtc);
