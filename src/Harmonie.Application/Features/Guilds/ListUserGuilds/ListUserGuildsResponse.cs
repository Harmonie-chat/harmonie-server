using Harmonie.Application.Features.Guilds;

namespace Harmonie.Application.Features.Guilds.ListUserGuilds;

public sealed record ListUserGuildsResponse(
    IReadOnlyList<ListUserGuildsItemResponse> Guilds);

public sealed record ListUserGuildsItemResponse(
    Guid GuildId,
    string Name,
    Guid OwnerUserId,
    Guid? IconFileId,
    GuildIconDto? Icon,
    string Role,
    DateTime JoinedAtUtc);
