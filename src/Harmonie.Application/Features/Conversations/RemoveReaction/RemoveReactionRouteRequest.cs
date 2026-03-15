namespace Harmonie.Application.Features.Conversations.RemoveReaction;

public sealed class RemoveReactionRouteRequest
{
    public string? ConversationId { get; init; }
    public string? MessageId { get; init; }
    public string? Emoji { get; init; }
}
