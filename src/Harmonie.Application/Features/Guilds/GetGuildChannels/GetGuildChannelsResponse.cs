namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed record GetGuildChannelsResponse(
    string GuildId,
    IReadOnlyList<GetGuildChannelsItemResponse> Channels);

public sealed record GetGuildChannelsItemResponse(
    string ChannelId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
