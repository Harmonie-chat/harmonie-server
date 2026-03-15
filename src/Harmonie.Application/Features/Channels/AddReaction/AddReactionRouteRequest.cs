namespace Harmonie.Application.Features.Channels.AddReaction;

public sealed class AddReactionRouteRequest
{
    public string? ChannelId { get; init; }
    public string? MessageId { get; init; }
    public string? Emoji { get; init; }
}
