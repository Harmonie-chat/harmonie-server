namespace Harmonie.Infrastructure.Rows;

public sealed class GuildBanWithUserRow
{
    public Guid UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public Guid? AvatarFileId { get; init; }

    public string? AvatarColor { get; init; }

    public string? AvatarIcon { get; init; }

    public string? AvatarBg { get; init; }

    public string? Reason { get; init; }

    public Guid BannedBy { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
