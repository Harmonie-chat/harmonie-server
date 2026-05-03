namespace Harmonie.Infrastructure.Rows.Messages;

internal sealed class PinnedMessageRow
{
    public Guid MessageId { get; init; }
    public Guid PinnedByUserId { get; init; }
    public DateTime PinnedAtUtc { get; init; }
}
