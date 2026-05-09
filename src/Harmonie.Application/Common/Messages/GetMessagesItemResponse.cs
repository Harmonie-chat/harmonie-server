using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Single message item returned by GetMessages across all scopes.
/// </summary>
public sealed record GetMessagesItemResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    IReadOnlyList<MessageReactionDto> Reactions,
    IReadOnlyList<LinkPreviewDto>? LinkPreviews,
    bool IsPinned,
    ReplyPreviewDto? ReplyTo,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
