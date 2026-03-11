using Harmonie.Application.Features.Guilds;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public sealed record UpdateGuildResponse(
    string GuildId,
    string Name,
    string OwnerUserId,
    string? IconFileId,
    GuildIconDto? Icon);
