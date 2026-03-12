namespace Harmonie.Infrastructure.Rows;

public sealed class GuildInviteRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid GuildId { get; init; }
    public Guid CreatorId { get; init; }
    public int? MaxUses { get; init; }
    public int UsesCount { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
