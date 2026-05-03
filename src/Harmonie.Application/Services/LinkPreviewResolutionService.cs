using System.Text.RegularExpressions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Services;

public sealed class LinkPreviewResolutionService
{
    private static readonly TimeSpan PreviewCacheMaxAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex HrefRegex = new(
        @"href\s*=\s*[""'](https?://[^""'\s>]+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));
    private const int MaxUrlsPerMessage = 5;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<LinkPreviewResolutionService> _logger;

    public LinkPreviewResolutionService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LinkPreviewResolutionService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public IReadOnlyList<Uri> ParseUrls(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<Uri>();

        var urls = new List<Uri>(MaxUrlsPerMessage);

        // First pass: split by whitespace (plain text)
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

        // Second pass: extract href from <a> tags (HTML content from rich text editors)
        if (urls.Count == 0 && content.Contains("<a ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                foreach (Match match in HrefRegex.Matches(content))
                {
                    if (urls.Count >= MaxUrlsPerMessage)
                        break;

                    var href = match.Groups[1].Value;
                    if (Uri.TryCreate(href, UriKind.Absolute, out var uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        && IsSafeHost(uri)
                        && !urls.Contains(uri))
                    {
                        urls.Add(uri);
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Malformed HTML — skip
            }
        }

        return urls;
    }

    public async Task ResolveAndNotifyForChannelAsync(
        MessageId messageId,
        GuildChannelId channelId,
        string channelName,
        GuildId guildId,
        string guildName,
        IReadOnlyList<Uri> urls,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ResolutionTimeout);

            using var scope = _serviceScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILinkPreviewRepository>();
            var fetcher = scope.ServiceProvider.GetRequiredService<ILinkPreviewFetcher>();
            var notifier = scope.ServiceProvider.GetRequiredService<ITextChannelNotifier>();

            var previews = await ResolveAsync(messageId, urls, repo, fetcher, cts.Token);

            if (previews.Count > 0)
            {
                await BestEffortNotificationHelper.TryNotifyAsync(
                    token => notifier.NotifyMessagePreviewUpdatedAsync(
                        new TextChannelMessagePreviewUpdatedNotification(messageId, channelId, channelName, guildId, guildName, previews),
                        token),
                    NotificationTimeout,
                    _logger,
                    "Link preview channel notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
                    messageId,
                    channelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Link preview channel resolution failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
                messageId,
                channelId);
        }
    }

    public async Task ResolveAndNotifyForConversationAsync(
        MessageId messageId,
        ConversationId conversationId,
        IReadOnlyList<Uri> urls,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ResolutionTimeout);

            using var scope = _serviceScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILinkPreviewRepository>();
            var fetcher = scope.ServiceProvider.GetRequiredService<ILinkPreviewFetcher>();
            var notifier = scope.ServiceProvider.GetRequiredService<IConversationMessageNotifier>();

            var previews = await ResolveAsync(messageId, urls, repo, fetcher, cts.Token);

            if (previews.Count > 0)
            {
                await BestEffortNotificationHelper.TryNotifyAsync(
                    token => notifier.NotifyMessagePreviewUpdatedAsync(
                        new ConversationMessagePreviewUpdatedNotification(messageId, conversationId, previews),
                        token),
                    NotificationTimeout,
                    _logger,
                    "Link preview conversation notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
                    messageId,
                    conversationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Link preview conversation resolution failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
                messageId,
                conversationId);
        }
    }

    private async Task<IReadOnlyList<LinkPreviewDto>> ResolveAsync(
        MessageId messageId,
        IReadOnlyList<Uri> urls,
        ILinkPreviewRepository repo,
        ILinkPreviewFetcher fetcher,
        CancellationToken cancellationToken)
    {
        var previews = new List<LinkPreviewDto>(urls.Count);

        foreach (var url in urls)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var preview = await ResolveSingleUrlAsync(messageId, url, repo, fetcher, cancellationToken);
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

    private static async Task<LinkPreviewDto?> ResolveSingleUrlAsync(
        MessageId messageId,
        Uri url,
        ILinkPreviewRepository repo,
        ILinkPreviewFetcher fetcher,
        CancellationToken cancellationToken)
    {
        var urlText = url.ToString();

        var cached = await repo.TryGetRecentPreviewAsync(
            urlText, PreviewCacheMaxAge, cancellationToken);
        if (cached is not null)
        {
            await repo.AddAsync(
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

        var metadata = await fetcher.FetchAsync(url, cancellationToken);
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

        await repo.AddAsync(preview, cancellationToken);

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
