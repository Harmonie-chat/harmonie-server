namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed record CreateChannelResponse(
    Guid ChannelId,
    Guid GuildId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
