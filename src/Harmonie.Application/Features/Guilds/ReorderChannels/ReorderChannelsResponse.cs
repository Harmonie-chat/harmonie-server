namespace Harmonie.Application.Features.Guilds.ReorderChannels;

public sealed record ReorderChannelsResponse(
    Guid GuildId,
    IReadOnlyList<ReorderChannelsItemResponse> Channels);

public sealed record ReorderChannelsItemResponse(
    Guid ChannelId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
