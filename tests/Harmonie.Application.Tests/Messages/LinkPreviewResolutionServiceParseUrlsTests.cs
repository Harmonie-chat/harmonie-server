using FluentAssertions;
using Harmonie.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class LinkPreviewResolutionServiceParseUrlsTests
{
    private readonly LinkPreviewResolutionService _service;

    public LinkPreviewResolutionServiceParseUrlsTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _service = new LinkPreviewResolutionService(
            scopeFactoryMock.Object,
            NullLogger<LinkPreviewResolutionService>.Instance);
    }

    [Fact]
    public void ParseUrls_WhenContentIsNull_ShouldReturnEmpty()
    {
        var result = _service.ParseUrls(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentIsEmpty_ShouldReturnEmpty()
    {
        var result = _service.ParseUrls("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentIsWhitespace_ShouldReturnEmpty()
    {
        var result = _service.ParseUrls("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentHasNoUrls_ShouldReturnEmpty()
    {
        var result = _service.ParseUrls("Hello world, how are you?");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentHasOneHttpsUrl_ShouldReturnIt()
    {
        var result = _service.ParseUrls("Check this out: https://example.com/article");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://example.com/article");
    }

    [Fact]
    public void ParseUrls_WhenContentHasOneHttpUrl_ShouldReturnIt()
    {
        var result = _service.ParseUrls("See http://example.com");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("http://example.com/");
    }

    [Fact]
    public void ParseUrls_WhenContentHasMultipleUrls_ShouldReturnAllUpToMax()
    {
        var result = _service.ParseUrls(
            "https://a.com https://b.com https://c.com https://d.com https://e.com https://f.com");

        result.Should().HaveCount(5);
        result[0].ToString().Should().Be("https://a.com/");
        result[4].ToString().Should().Be("https://e.com/");
    }

    [Fact]
    public void ParseUrls_WhenUrlHasFtpScheme_ShouldBeIgnored()
    {
        var result = _service.ParseUrls("ftp://files.example.com https://web.example.com");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://web.example.com/");
    }

    [Fact]
    public void ParseUrls_WhenUrlIsRelative_ShouldBeIgnored()
    {
        var result = _service.ParseUrls("Go to /relative/path");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenUrlIsMixedWithText_ShouldExtractCorrectly()
    {
        var result = _service.ParseUrls(
            "Hey, look at https://example.com/page?q=1#section it's great!");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://example.com/page?q=1#section");
    }

    [Fact]
    public void ParseUrls_WhenHtmlAnchorTag_ShouldExtractHref()
    {
        var content = "<p><a href=\"https://www.youtube.com/watch?v=nUp_bv_sOeI\" rel=\"noopener noreferrer\" target=\"_blank\">https://www.youtube.com/watch?v=nUp_bv_sOeI</a></p>";

        var result = _service.ParseUrls(content);

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://www.youtube.com/watch?v=nUp_bv_sOeI");
    }

    [Fact]
    public void ParseUrls_WhenHtmlWithMultipleAnchors_ShouldExtractAll()
    {
        var content = "<a href=\"https://a.com\">a</a> <a href='https://b.com'>b</a>";

        var result = _service.ParseUrls(content);

        result.Should().HaveCount(2);
        result[0].ToString().Should().Be("https://a.com/");
        result[1].ToString().Should().Be("https://b.com/");
    }

    [Fact]
    public void ParseUrls_WhenPlainTextHasUrl_PrefersPlainTextOverHtml()
    {
        var content = "https://plain.com <a href=\"https://html.com\">link</a>";

        var result = _service.ParseUrls(content);

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://plain.com/");
    }

    [Fact]
    public void ParseUrls_WhenHtmlAnchorHasNoHttps_ShouldBeIgnored()
    {
        var content = "<a href=\"ftp://files.com\">files</a>";

        var result = _service.ParseUrls(content);

        result.Should().BeEmpty();
    }
}
