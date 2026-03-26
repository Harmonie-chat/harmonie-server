namespace Harmonie.Infrastructure.Rows.Conversations;

public sealed class UserConversationSummaryRow
{
    public Guid ConversationId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string? Name { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public Guid ParticipantUserId { get; init; }

    public string ParticipantUsername { get; init; } = string.Empty;
}
