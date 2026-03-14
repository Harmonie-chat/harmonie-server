namespace Harmonie.Infrastructure.Rows;

public sealed class ConversationMessageSearchRow
{
    public Guid MessageId { get; init; }

    public Guid AuthorUserId { get; init; }

    public string AuthorUsername { get; init; } = string.Empty;

    public string? AuthorDisplayName { get; init; }

    public Guid? AuthorAvatarFileId { get; init; }

    public string? AuthorAvatarColor { get; init; }

    public string? AuthorAvatarIcon { get; init; }

    public string? AuthorAvatarBg { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
