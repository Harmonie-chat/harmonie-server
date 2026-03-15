namespace Harmonie.Application.Features.Conversations.AddReaction;

public sealed class AddReactionRouteRequest
{
    public string? ConversationId { get; init; }
    public string? MessageId { get; init; }
    public string? Emoji { get; init; }
}
