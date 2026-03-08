namespace Harmonie.Application.Features.Guilds.SearchMessages;

public sealed class SearchMessagesRequest
{
    public string? Q { get; init; }

    public string? ChannelId { get; init; }

    public string? AuthorId { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
