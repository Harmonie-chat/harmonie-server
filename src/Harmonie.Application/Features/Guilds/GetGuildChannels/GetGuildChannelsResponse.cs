namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed record GetGuildChannelsResponse(
    Guid GuildId,
    IReadOnlyList<GetGuildChannelsItemResponse> Channels);

public sealed record GetGuildChannelsItemResponse(
    Guid ChannelId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
