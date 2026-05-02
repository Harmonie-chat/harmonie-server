using Harmonie.Application.Interfaces.Messages;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Harmonie.Infrastructure.Services;

public sealed class LinkPreviewFetcher : ILinkPreviewFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkPreviewFetcher> _logger;

    public LinkPreviewFetcher(HttpClient httpClient, ILogger<LinkPreviewFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LinkPreviewMetadata?> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        string html;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(request, cancellationToken);
            var contentType = headResponse.Content.Headers.ContentType?.MediaType;
            if (contentType is not null && !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HEAD request failed for URL. Url={Url}", url);
            return null;
        }

        try
        {
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
            using var getResponse = await _httpClient.SendAsync(getRequest, cancellationToken);
            getResponse.EnsureSuccessStatusCode();

            html = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GET request failed for URL. Url={Url}", url);
            return null;
        }

        return ExtractMetadata(url.ToString(), html);
    }

    private static LinkPreviewMetadata? ExtractMetadata(string url, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = ExtractMeta(doc, "og:title")
                    ?? ExtractMeta(doc, "twitter:title")
                    ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

        var description = ExtractMeta(doc, "og:description")
                          ?? ExtractMeta(doc, "twitter:description")
                          ?? ExtractMeta(doc, "description", "name");

        var imageUrl = ExtractMeta(doc, "og:image")
                       ?? ExtractMeta(doc, "twitter:image");

        var siteName = ExtractMeta(doc, "og:site_name");

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
            return null;

        return new LinkPreviewMetadata(url, NullIfEmpty(title), NullIfEmpty(description), NullIfEmpty(imageUrl), NullIfEmpty(siteName));
    }

    private static string? ExtractMeta(HtmlDocument doc, string property, string attribute = "property")
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//meta[@{attribute}='{property}']");
        return node?.GetAttributeValue("content", string.Empty) is { Length: > 0 } value ? value : null;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
