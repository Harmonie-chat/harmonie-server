namespace Harmonie.Infrastructure.Rows;

public sealed class GuildRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid OwnerUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
