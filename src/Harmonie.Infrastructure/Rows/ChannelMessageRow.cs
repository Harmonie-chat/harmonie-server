namespace Harmonie.Infrastructure.Rows;

public sealed class ChannelMessageRow
{
    public Guid Id { get; init; }

    public Guid ChannelId { get; init; }

    public Guid AuthorUserId { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
}
