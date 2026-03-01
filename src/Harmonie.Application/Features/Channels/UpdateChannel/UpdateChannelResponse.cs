namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed record UpdateChannelResponse(
    string ChannelId,
    string GuildId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
