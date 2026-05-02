namespace Harmonie.Infrastructure.Rows.Messages;

public sealed class MessageLinkPreviewRow
{
    public Guid MessageId { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public string? SiteName { get; init; }

    public DateTime FetchedAtUtc { get; init; }
}
