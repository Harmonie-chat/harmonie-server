using FluentAssertions;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class GuildChannelTests
{
    [Fact]
    public void UpdateName_ShouldMarkChannelAsUpdated()
    {
        var channel = CreateChannel();

        var result = channel.UpdateName("updated-name", TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        channel.Name.Should().Be("updated-name");
        channel.UpdatedAtUtc.Should().Be(TestTime.UtcNow);
    }

    [Fact]
    public void UpdatePosition_ShouldMarkChannelAsUpdated()
    {
        var channel = CreateChannel();

        var result = channel.UpdatePosition(4, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        channel.Position.Should().Be(4);
        channel.UpdatedAtUtc.Should().Be(TestTime.UtcNow);
    }

    private static GuildChannel CreateChannel()
    {
        var channelResult = GuildChannel.Create(
            GuildId.New(),
            "general",
            GuildChannelType.Text,
            isDefault: true,
            position: 0,
            createdAtUtc: TestTime.UtcNow);

        if (channelResult.IsFailure || channelResult.Value is null)
            throw new InvalidOperationException("Failed to create guild channel for tests.");

        return channelResult.Value;
    }
}
