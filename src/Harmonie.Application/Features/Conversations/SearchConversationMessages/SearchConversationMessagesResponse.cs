namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed record SearchConversationMessagesResponse(
    string ConversationId,
    IReadOnlyList<SearchConversationMessagesItemResponse> Items,
    string? NextCursor);

public sealed record SearchConversationMessagesItemResponse(
    string MessageId,
    string AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
