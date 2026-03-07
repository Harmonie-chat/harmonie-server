namespace Harmonie.Application.Features.Conversations.GetDirectMessages;

public sealed record GetDirectMessagesResponse(
    string ConversationId,
    IReadOnlyList<GetDirectMessagesItemResponse> Items,
    string? NextCursor);

public sealed record GetDirectMessagesItemResponse(
    string MessageId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
