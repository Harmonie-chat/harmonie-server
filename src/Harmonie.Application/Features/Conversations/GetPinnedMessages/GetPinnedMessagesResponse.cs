using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Conversations.GetPinnedMessages;

public sealed record GetConversationPinnedMessagesResponse(
    Guid ConversationId,
    IReadOnlyList<GetPinnedMessagesItemResponse> Items);

public sealed record GetPinnedMessagesItemResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    IReadOnlyList<MessageReactionDto> Reactions,
    IReadOnlyList<LinkPreviewDto>? LinkPreviews,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid PinnedByUserId,
    DateTime PinnedAtUtc);
