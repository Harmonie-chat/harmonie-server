namespace Harmonie.Application.Features.Conversations.DeleteDirectMessage;

public sealed class DeleteDirectMessageRouteRequest
{
    public string? ConversationId { get; init; }

    public string? MessageId { get; init; }
}
