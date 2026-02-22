namespace Harmonie.Infrastructure.Dto;

public sealed class GuildChannelDto
{
    public Guid Id { get; init; }

    public Guid GuildId { get; init; }

    public string Name { get; init; } = string.Empty;

    public short Type { get; init; }

    public bool IsDefault { get; init; }

    public int Position { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
