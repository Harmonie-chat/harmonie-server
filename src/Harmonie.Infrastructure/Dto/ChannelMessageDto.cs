namespace Harmonie.Infrastructure.Dto;

public sealed class ChannelMessageDto
{
    public Guid Id { get; init; }

    public Guid ChannelId { get; init; }

    public Guid AuthorUserId { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}
