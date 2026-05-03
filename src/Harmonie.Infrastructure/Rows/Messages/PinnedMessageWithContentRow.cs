namespace Harmonie.Infrastructure.Rows.Messages;

internal sealed class PinnedMessageWithContentRow
{
    public Guid Id { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorUsername { get; init; } = string.Empty;
    public string? AuthorDisplayName { get; init; }
    public string? Content { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
    public Guid PinnedByUserId { get; init; }
    public DateTime PinnedAtUtc { get; init; }
}
