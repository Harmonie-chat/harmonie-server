using FluentAssertions;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class UserStatusTests
{
    [Theory]
    [InlineData("online")]
    [InlineData("idle")]
    [InlineData("dnd")]
    [InlineData("invisible")]
    public void Create_WithValidValue_ShouldSucceed(string status)
    {
        var result = UserStatus.Create(status);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be(status);
    }

    [Fact]
    public void Create_WithInvalidValue_ShouldFail()
    {
        var result = UserStatus.Create("away");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status must be one of: online, idle, dnd, invisible");
    }

    [Fact]
    public void Create_WithEmptyValue_ShouldFail()
    {
        var result = UserStatus.Create("");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status cannot be empty");
    }

    [Fact]
    public void Create_WithWhitespace_ShouldFail()
    {
        var result = UserStatus.Create("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status cannot be empty");
    }

    [Fact]
    public void Create_ShouldNormalizeCase()
    {
        var result = UserStatus.Create("DND");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("dnd");
    }

    [Theory]
    [InlineData("online")]
    [InlineData("idle")]
    [InlineData("dnd")]
    [InlineData("invisible")]
    public void StaticMembers_ShouldHaveExpectedValue(string expected)
    {
        var status = UserStatus.Create(expected).Value!;

        status.Value.Should().Be(expected);
    }

    [Fact]
    public void ImplicitConversion_ShouldReturnValueString()
    {
        string statusString = UserStatus.Dnd;

        statusString.Should().Be("dnd");
    }

    [Fact]
    public void SameValue_ShouldBeEqual()
    {
        var a = UserStatus.Create("online").Value!;
        var b = UserStatus.Online;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void DifferentValue_ShouldNotBeEqual()
    {
        var a = UserStatus.Online;
        var b = UserStatus.Idle;

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var status = UserStatus.Dnd;

        status.ToString().Should().Be("dnd");
    }
}
