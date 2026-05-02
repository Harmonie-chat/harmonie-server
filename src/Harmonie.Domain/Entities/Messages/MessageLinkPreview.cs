using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Messages;

namespace Harmonie.Domain.Entities.Messages;

public sealed class MessageLinkPreview
{
    public MessageId MessageId { get; }

    public string Url { get; }

    public string? Title { get; }

    public string? Description { get; }

    public string? ImageUrl { get; }

    public string? SiteName { get; }

    public DateTime FetchedAtUtc { get; }

    private MessageLinkPreview(
        MessageId messageId,
        string url,
        string? title,
        string? description,
        string? imageUrl,
        string? siteName,
        DateTime fetchedAtUtc)
    {
        MessageId = messageId;
        Url = url;
        Title = title;
        Description = description;
        ImageUrl = imageUrl;
        SiteName = siteName;
        FetchedAtUtc = fetchedAtUtc;
    }

    public static Result<MessageLinkPreview> Create(
        MessageId messageId,
        string url,
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        string? siteName = null)
    {
        if (messageId is null)
            return Result.Failure<MessageLinkPreview>("Message ID is required");

        if (string.IsNullOrWhiteSpace(url))
            return Result.Failure<MessageLinkPreview>("URL is required");

        return Result.Success(new MessageLinkPreview(
            messageId,
            url,
            title,
            description,
            imageUrl,
            siteName,
            DateTime.UtcNow));
    }

    public static MessageLinkPreview Rehydrate(
        MessageId messageId,
        string url,
        string? title,
        string? description,
        string? imageUrl,
        string? siteName,
        DateTime fetchedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return new MessageLinkPreview(
            messageId,
            url,
            title,
            description,
            imageUrl,
            siteName,
            fetchedAtUtc);
    }
}
