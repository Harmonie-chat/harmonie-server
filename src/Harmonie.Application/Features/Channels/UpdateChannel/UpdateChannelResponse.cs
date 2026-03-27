namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed record UpdateChannelResponse(
    Guid ChannelId,
    Guid GuildId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
