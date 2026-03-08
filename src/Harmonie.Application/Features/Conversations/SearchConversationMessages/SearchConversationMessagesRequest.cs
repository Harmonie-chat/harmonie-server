namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed class SearchConversationMessagesRequest
{
    public string? Q { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
