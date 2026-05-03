using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Conversations.GetPinnedMessages;

public sealed record GetConversationPinnedMessagesResponse(
    Guid ConversationId,
    IReadOnlyList<GetPinnedMessagesItemResponse> Items,
    string? NextCursor);

public sealed record GetPinnedMessagesItemResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid PinnedByUserId,
    DateTime PinnedAtUtc);
