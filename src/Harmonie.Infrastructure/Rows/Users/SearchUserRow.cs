namespace Harmonie.Infrastructure.Rows.Users;

public sealed class SearchUserRow
{
    public Guid UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public Guid? AvatarFileId { get; init; }

    public string? AvatarColor { get; init; }

    public string? AvatarIcon { get; init; }

    public string? AvatarBg { get; init; }

    public string? Bio { get; init; }

    public bool IsActive { get; init; }
}
