namespace Harmonie.Infrastructure.Rows;

public sealed class UserGuildMembershipRow
{
    public Guid GuildId { get; init; }

    public string GuildName { get; init; } = string.Empty;

    public Guid OwnerUserId { get; init; }

    public Guid? IconFileId { get; init; }

    public string? IconColor { get; init; }

    public string? IconName { get; init; }

    public string? IconBg { get; init; }

    public DateTime GuildCreatedAtUtc { get; init; }

    public DateTime GuildUpdatedAtUtc { get; init; }

    public short Role { get; init; }

    public DateTime JoinedAtUtc { get; init; }
}
