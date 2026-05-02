using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Messages.ResolveLinkPreviews;

public sealed class LinkPreviewResolutionService
{
    private static readonly TimeSpan PreviewCacheMaxAge = TimeSpan.FromHours(24);
    private const int MaxUrlsPerMessage = 5;

    private readonly ILinkPreviewRepository _linkPreviewRepository;
    private readonly ILinkPreviewFetcher _linkPreviewFetcher;
    private readonly ILogger<LinkPreviewResolutionService> _logger;

    public LinkPreviewResolutionService(
        ILinkPreviewRepository linkPreviewRepository,
        ILinkPreviewFetcher linkPreviewFetcher,
        ILogger<LinkPreviewResolutionService> logger)
    {
        _linkPreviewRepository = linkPreviewRepository;
        _linkPreviewFetcher = linkPreviewFetcher;
        _logger = logger;
    }

    public static IReadOnlyList<Uri> ParseUrls(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<Uri>();

        var urls = new List<Uri>(MaxUrlsPerMessage);

        foreach (var token in content.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (urls.Count >= MaxUrlsPerMessage)
                break;

            if (!Uri.TryCreate(token, UriKind.Absolute, out var uri))
                continue;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                continue;

            if (!IsSafeHost(uri))
                continue;

            urls.Add(uri);
        }

        return urls;
    }

    public async Task<IReadOnlyList<LinkPreviewDto>> ResolveForMessageAsync(
        MessageId messageId,
        IReadOnlyList<Uri> urls,
        CancellationToken cancellationToken = default)
    {
        var previews = new List<LinkPreviewDto>(urls.Count);

        foreach (var url in urls)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var preview = await ResolveSingleUrlAsync(messageId, url, cancellationToken);
                if (preview is not null)
                    previews.Add(preview);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to resolve link preview. MessageId={MessageId}, Url={Url}",
                    messageId,
                    url);
            }
        }

        return previews;
    }

    private async Task<LinkPreviewDto?> ResolveSingleUrlAsync(
        MessageId messageId,
        Uri url,
        CancellationToken cancellationToken)
    {
        var urlText = url.ToString();

        var cached = await _linkPreviewRepository.TryGetRecentPreviewAsync(
            urlText, PreviewCacheMaxAge, cancellationToken);
        if (cached is not null)
        {
            await _linkPreviewRepository.AddAsync(
                MessageLinkPreview.Rehydrate(
                    messageId,
                    urlText,
                    cached.Title,
                    cached.Description,
                    cached.ImageUrl,
                    cached.SiteName,
                    DateTime.UtcNow),
                cancellationToken);
            return MapToDto(urlText, cached.Title, cached.Description, cached.ImageUrl, cached.SiteName);
        }

        var metadata = await _linkPreviewFetcher.FetchAsync(url, cancellationToken);
        if (metadata is null)
            return null;

        var resolvedUrl = metadata.Url;
        var preview = MessageLinkPreview.Rehydrate(
            messageId,
            resolvedUrl,
            metadata.Title,
            metadata.Description,
            metadata.ImageUrl,
            metadata.SiteName,
            DateTime.UtcNow);

        await _linkPreviewRepository.AddAsync(preview, cancellationToken);

        return MapToDto(resolvedUrl, metadata.Title, metadata.Description, metadata.ImageUrl, metadata.SiteName);
    }

    private static LinkPreviewDto MapToDto(
        string url,
        string? title,
        string? description,
        string? imageUrl,
        string? siteName)
        => new(url, title, description, imageUrl, siteName);

    private static bool IsSafeHost(Uri uri)
    {
        var host = uri.DnsSafeHost;
        if (string.IsNullOrEmpty(host))
            return false;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
