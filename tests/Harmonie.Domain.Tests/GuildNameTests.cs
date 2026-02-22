using FluentAssertions;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class GuildNameTests
{
    [Theory]
    [InlineData("Harmonie Team", "Harmonie Team")]
    [InlineData("  My Guild  ", "My Guild")]
    [InlineData("abc", "abc")]
    public void Create_WithValidName_ShouldSucceed(string rawName, string expectedName)
    {
        var result = GuildName.Create(rawName);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Value.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Create_WithInvalidName_ShouldFail(string? rawName)
    {
        var result = GuildName.Create(rawName);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();
    }
}
