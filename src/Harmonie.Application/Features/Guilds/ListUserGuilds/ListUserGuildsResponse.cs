namespace Harmonie.Application.Features.Guilds.ListUserGuilds;

public sealed record ListUserGuildsResponse(
    IReadOnlyList<ListUserGuildsItemResponse> Guilds);

public sealed record ListUserGuildsItemResponse(
    string GuildId,
    string Name,
    string OwnerUserId,
    string Role,
    DateTime JoinedAtUtc);
