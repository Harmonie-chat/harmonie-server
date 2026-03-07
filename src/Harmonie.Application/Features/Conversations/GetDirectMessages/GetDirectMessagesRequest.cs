namespace Harmonie.Application.Features.Conversations.GetDirectMessages;

public sealed class GetDirectMessagesRequest
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
