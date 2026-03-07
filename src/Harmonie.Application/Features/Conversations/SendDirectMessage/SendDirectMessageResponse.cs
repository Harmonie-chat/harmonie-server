namespace Harmonie.Application.Features.Conversations.SendDirectMessage;

public sealed record SendDirectMessageResponse(
    string MessageId,
    string ConversationId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
