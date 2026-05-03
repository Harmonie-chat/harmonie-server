namespace Harmonie.Application.Common.Messages;

public sealed record ReplyPreviewDto(
    Guid MessageId,
    Guid AuthorUserId,
    string? AuthorDisplayName,
    string AuthorUsername,
    string? Content,
    bool HasAttachments,
    bool IsDeleted,
    DateTime? DeletedAtUtc);
