namespace Harmonie.Application.Features.Conversations.EditDirectMessage;

public sealed record EditDirectMessageResponse(
    string MessageId,
    string ConversationId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
