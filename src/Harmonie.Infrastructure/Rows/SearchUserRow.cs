namespace Harmonie.Infrastructure.Rows;

public sealed class SearchUserRow
{
    public Guid UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public bool IsActive { get; init; }
}
