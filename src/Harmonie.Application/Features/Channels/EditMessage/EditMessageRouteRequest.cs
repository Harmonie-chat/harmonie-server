namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed class EditMessageRouteRequest
{
    public string? ChannelId { get; init; }
    public string? MessageId { get; init; }
}
