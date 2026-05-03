namespace Harmonie.Infrastructure.Rows.Messages;

public sealed class MessageRow
{
    public Guid Id { get; init; }

    public Guid? ChannelId { get; init; }

    public Guid? ConversationId { get; init; }

    public Guid AuthorUserId { get; init; }

    public string? Content { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public Guid? ReplyToMessageId { get; init; }

    public DateTime? DeletedAtUtc { get; init; }
}
