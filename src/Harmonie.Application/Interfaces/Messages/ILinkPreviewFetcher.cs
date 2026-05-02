namespace Harmonie.Application.Interfaces.Messages;

public sealed record LinkPreviewMetadata(
    string Url,
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName);

public interface ILinkPreviewFetcher
{
    Task<LinkPreviewMetadata?> FetchAsync(Uri url, CancellationToken cancellationToken = default);
}
