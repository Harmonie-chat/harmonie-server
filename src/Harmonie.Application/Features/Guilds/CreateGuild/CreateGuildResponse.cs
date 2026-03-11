using Harmonie.Application.Features.Guilds;

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed record CreateGuildResponse(
    string GuildId,
    string Name,
    string OwnerUserId,
    string? IconFileId,
    GuildIconDto? Icon,
    string DefaultTextChannelId,
    string DefaultVoiceChannelId,
    DateTime CreatedAtUtc);
