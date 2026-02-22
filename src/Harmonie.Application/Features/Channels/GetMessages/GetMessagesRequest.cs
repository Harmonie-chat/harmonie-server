namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed class GetMessagesRequest
{
    public string? Before { get; init; }

    public int? Limit { get; init; }
}
