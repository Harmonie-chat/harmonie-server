namespace Harmonie.Application.Features.Conversations.EditDirectMessage;

public sealed class EditDirectMessageRouteRequest
{
    public string? ConversationId { get; init; }

    public string? MessageId { get; init; }
}
