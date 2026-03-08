using FluentAssertions;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class MessageContentTests
{
    [Fact]
    public void Create_WithValidContent_ShouldTrimAndReturnSuccess()
    {
        const string rawContent = "  hello world  ";

        var result = MessageContent.Create(rawContent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Value.Should().Be("hello world");
    }

    [Fact]
    public void Create_WithNullContent_ShouldReturnFailure()
    {
        var result = MessageContent.Create(null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Message content is required");
    }

    [Fact]
    public void Create_WithWhitespaceContent_ShouldReturnFailure()
    {
        const string rawContent = "   ";

        var result = MessageContent.Create(rawContent);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Message content cannot be empty");
    }

    [Fact]
    public void Create_WithTooLongContent_ShouldReturnFailure()
    {
        var tooLongContent = new string('a', MessageContent.MaxLength + 1);

        var result = MessageContent.Create(tooLongContent);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Message content cannot exceed {MessageContent.MaxLength} characters");
    }
}
