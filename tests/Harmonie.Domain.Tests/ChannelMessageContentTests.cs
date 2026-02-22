using FluentAssertions;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ChannelMessageContentTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  hello world  ", "hello world")]
    public void Create_WithValidContent_ShouldSucceed(string rawContent, string normalizedContent)
    {
        var result = ChannelMessageContent.Create(rawContent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Value.Should().Be(normalizedContent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyContent_ShouldFail(string? rawContent)
    {
        var result = ChannelMessageContent.Create(rawContent);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_WithTooLongContent_ShouldFail()
    {
        var tooLongContent = new string('a', ChannelMessageContent.MaxLength + 1);

        var result = ChannelMessageContent.Create(tooLongContent);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();
    }
}
