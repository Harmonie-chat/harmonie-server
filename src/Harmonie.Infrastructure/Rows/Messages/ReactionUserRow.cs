namespace Harmonie.Infrastructure.Rows.Messages;

internal sealed class ReactionUserRow
{
    public Guid MessageId { get; init; }
    public string Emoji { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}
