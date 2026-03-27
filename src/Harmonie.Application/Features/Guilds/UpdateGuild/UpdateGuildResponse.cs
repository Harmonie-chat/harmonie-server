using Harmonie.Application.Features.Guilds;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public sealed record UpdateGuildResponse(
    Guid GuildId,
    string Name,
    Guid OwnerUserId,
    Guid? IconFileId,
    GuildIconDto? Icon);
