namespace Harmonie.Infrastructure.Rows.Messages;

internal sealed class ReplyPreviewRow
{
    public Guid TargetMessageId { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorUsername { get; init; } = string.Empty;
    public string? AuthorDisplayName { get; init; }
    public string? Content { get; init; }
    public bool HasAttachments { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
}
