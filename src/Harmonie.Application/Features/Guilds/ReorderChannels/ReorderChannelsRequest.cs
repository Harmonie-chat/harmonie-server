namespace Harmonie.Application.Features.Guilds.ReorderChannels;

public sealed record ReorderChannelsRequest(
    IReadOnlyList<ReorderChannelsItemRequest> Channels);

public sealed record ReorderChannelsItemRequest(
    string ChannelId,
    int Position);
