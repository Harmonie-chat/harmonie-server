namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed record CreateChannelRequest(string Name, ChannelTypeInput Type, int Position);
