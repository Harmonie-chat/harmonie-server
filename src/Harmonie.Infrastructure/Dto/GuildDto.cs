namespace Harmonie.Infrastructure.Dto;

public sealed class GuildDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid OwnerUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
