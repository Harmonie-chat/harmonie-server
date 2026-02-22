namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed record CreateGuildResponse(
    string GuildId,
    string Name,
    string OwnerUserId,
    string DefaultTextChannelId,
    string DefaultVoiceChannelId,
    DateTime CreatedAtUtc);
