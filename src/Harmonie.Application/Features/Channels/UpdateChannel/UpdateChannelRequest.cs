namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed record UpdateChannelRequest(
    string? Name = null,
    int? Position = null);
