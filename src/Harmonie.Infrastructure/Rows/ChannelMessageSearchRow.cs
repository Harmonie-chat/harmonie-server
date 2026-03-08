namespace Harmonie.Infrastructure.Rows;

public sealed class ChannelMessageSearchRow
{
    public Guid MessageId { get; init; }

    public Guid ChannelId { get; init; }

    public string ChannelName { get; init; } = string.Empty;

    public Guid AuthorUserId { get; init; }

    public string AuthorUsername { get; init; } = string.Empty;

    public string? AuthorDisplayName { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
