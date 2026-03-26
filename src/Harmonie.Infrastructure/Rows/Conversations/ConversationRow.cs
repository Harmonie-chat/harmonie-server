namespace Harmonie.Infrastructure.Rows.Conversations;

public sealed class ConversationRow
{
    public Guid Id { get; init; }

    public string Type { get; init; } = string.Empty;

    public string? Name { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
