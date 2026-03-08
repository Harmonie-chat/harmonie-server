namespace Harmonie.Infrastructure.Rows;

public sealed class DirectMessageSearchRow
{
    public Guid MessageId { get; init; }

    public Guid AuthorUserId { get; init; }

    public string AuthorUsername { get; init; } = string.Empty;

    public string? AuthorDisplayName { get; init; }

    public string? AuthorAvatarUrl { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
