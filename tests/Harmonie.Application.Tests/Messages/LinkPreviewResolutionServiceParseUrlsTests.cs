using FluentAssertions;
using Harmonie.Application.Features.Messages.ResolveLinkPreviews;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class LinkPreviewResolutionServiceParseUrlsTests
{
    [Fact]
    public void ParseUrls_WhenContentIsNull_ShouldReturnEmpty()
    {
        var result = LinkPreviewResolutionService.ParseUrls(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentIsEmpty_ShouldReturnEmpty()
    {
        var result = LinkPreviewResolutionService.ParseUrls("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentIsWhitespace_ShouldReturnEmpty()
    {
        var result = LinkPreviewResolutionService.ParseUrls("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentHasNoUrls_ShouldReturnEmpty()
    {
        var result = LinkPreviewResolutionService.ParseUrls("Hello world, how are you?");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenContentHasOneHttpsUrl_ShouldReturnIt()
    {
        var result = LinkPreviewResolutionService.ParseUrls("Check this out: https://example.com/article");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://example.com/article");
    }

    [Fact]
    public void ParseUrls_WhenContentHasOneHttpUrl_ShouldReturnIt()
    {
        var result = LinkPreviewResolutionService.ParseUrls("See http://example.com");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("http://example.com/");
    }

    [Fact]
    public void ParseUrls_WhenContentHasMultipleUrls_ShouldReturnAllUpToMax()
    {
        var result = LinkPreviewResolutionService.ParseUrls(
            "https://a.com https://b.com https://c.com https://d.com https://e.com https://f.com");

        result.Should().HaveCount(5);
        result[0].ToString().Should().Be("https://a.com/");
        result[4].ToString().Should().Be("https://e.com/");
    }

    [Fact]
    public void ParseUrls_WhenUrlHasFtpScheme_ShouldBeIgnored()
    {
        var result = LinkPreviewResolutionService.ParseUrls("ftp://files.example.com https://web.example.com");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://web.example.com/");
    }

    [Fact]
    public void ParseUrls_WhenUrlIsRelative_ShouldBeIgnored()
    {
        var result = LinkPreviewResolutionService.ParseUrls("Go to /relative/path");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrls_WhenUrlIsMixedWithText_ShouldExtractCorrectly()
    {
        var result = LinkPreviewResolutionService.ParseUrls(
            "Hey, look at https://example.com/page?q=1#section it's great!");

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("https://example.com/page?q=1#section");
    }
}
