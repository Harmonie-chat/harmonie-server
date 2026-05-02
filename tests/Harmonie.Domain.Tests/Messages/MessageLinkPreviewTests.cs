using FluentAssertions;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Xunit;

namespace Harmonie.Domain.Tests.Messages;

public sealed class MessageLinkPreviewTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var messageId = MessageId.New();
        var result = MessageLinkPreview.Create(
            messageId,
            "https://example.com",
            "Example Title",
            "Example Description",
            "https://example.com/image.png",
            "Example Site");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.MessageId.Should().Be(messageId);
        result.Value.Url.Should().Be("https://example.com");
        result.Value.Title.Should().Be("Example Title");
        result.Value.Description.Should().Be("Example Description");
        result.Value.ImageUrl.Should().Be("https://example.com/image.png");
        result.Value.SiteName.Should().Be("Example Site");
        result.Value.FetchedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullMessageId_ShouldFail()
    {
        var result = MessageLinkPreview.Create(null!, "https://example.com");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Message ID");
    }

    [Fact]
    public void Create_WithEmptyUrl_ShouldFail()
    {
        var result = MessageLinkPreview.Create(MessageId.New(), "");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("URL");
    }

    [Fact]
    public void Create_WithWhitespaceUrl_ShouldFail()
    {
        var result = MessageLinkPreview.Create(MessageId.New(), "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("URL");
    }

    [Fact]
    public void Create_WithOnlyUrl_ShouldSucceed()
    {
        var result = MessageLinkPreview.Create(MessageId.New(), "https://example.com");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().BeNull();
        result.Value.Description.Should().BeNull();
        result.Value.ImageUrl.Should().BeNull();
        result.Value.SiteName.Should().BeNull();
    }

    [Fact]
    public void Rehydrate_WithValidData_ShouldReturnEntity()
    {
        var messageId = MessageId.New();
        var fetchedAt = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);

        var preview = MessageLinkPreview.Rehydrate(
            messageId,
            "https://example.com",
            "Title",
            "Description",
            "https://example.com/img.png",
            "Site",
            fetchedAt);

        preview.MessageId.Should().Be(messageId);
        preview.Url.Should().Be("https://example.com");
        preview.Title.Should().Be("Title");
        preview.Description.Should().Be("Description");
        preview.ImageUrl.Should().Be("https://example.com/img.png");
        preview.SiteName.Should().Be("Site");
        preview.FetchedAtUtc.Should().Be(fetchedAt);
    }

    [Fact]
    public void Rehydrate_WithNullMessageId_ShouldThrow()
    {
        var act = () => MessageLinkPreview.Rehydrate(
            null!,
            "https://example.com",
            null, null, null, null,
            DateTime.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rehydrate_WithNullUrl_ShouldThrow()
    {
        var act = () => MessageLinkPreview.Rehydrate(
            MessageId.New(),
            null!,
            null, null, null, null,
            DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
