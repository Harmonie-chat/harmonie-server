namespace Harmonie.Infrastructure.Rows;

public sealed class GuildMemberUserRow
{
    public Guid UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public Guid? AvatarFileId { get; init; }

    public string? Bio { get; init; }

    public string? AvatarColor { get; init; }

    public string? AvatarIcon { get; init; }

    public string? AvatarBg { get; init; }

    public bool IsActive { get; init; }

    public short Role { get; init; }

    public DateTime JoinedAtUtc { get; init; }
}
