using Harmonie.Application.Features.Guilds;

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed record CreateGuildResponse(
    Guid GuildId,
    string Name,
    Guid OwnerUserId,
    Guid? IconFileId,
    GuildIconDto? Icon,
    Guid DefaultTextChannelId,
    Guid DefaultVoiceChannelId,
    DateTime CreatedAtUtc);
