namespace Harmonie.Infrastructure.Rows;

internal sealed class ReactionSummaryRow
{
    public Guid MessageId { get; init; }
    public string Emoji { get; init; } = string.Empty;
    public int Count { get; init; }
    public bool ReactedByCaller { get; init; }
}
