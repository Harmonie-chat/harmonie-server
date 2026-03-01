namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed record CreateChannelResponse(
    string ChannelId,
    string GuildId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
