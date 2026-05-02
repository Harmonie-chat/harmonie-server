namespace Harmonie.Infrastructure.Rows.Messages;

internal sealed class ReactionUserDetailRow
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
