namespace Harmonie.Application.Features.Conversations.UpdateGroupConversation;

public sealed record UpdateGroupConversationResponse(
    Guid ConversationId,
    string? Name);
