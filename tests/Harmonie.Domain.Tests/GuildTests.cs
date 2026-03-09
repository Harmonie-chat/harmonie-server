using FluentAssertions;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class GuildTests
{
    [Fact]
    public void UpdateName_WithDifferentName_ShouldUpdateNameAndTimestamp()
    {
        var originalName = GuildName.Create("Original Guild").Value!;
        var updatedName = GuildName.Create("Updated Guild").Value!;
        var guildResult = Guild.Create(originalName, UserId.New());
        guildResult.IsSuccess.Should().BeTrue();
        guildResult.Value.Should().NotBeNull();

        var guild = guildResult.Value!;
        var updateResult = guild.UpdateName(updatedName);

        updateResult.IsSuccess.Should().BeTrue();
        guild.Name.Should().Be(updatedName);
        guild.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void UpdateName_WithSameName_ShouldSucceedWithoutChangingTimestamp()
    {
        var guildName = GuildName.Create("Stable Guild").Value!;
        var guild = Guild.Create(guildName, UserId.New()).Value!;
        var initialUpdatedAtUtc = guild.UpdatedAtUtc;

        var result = guild.UpdateName(guildName);

        result.IsSuccess.Should().BeTrue();
        guild.UpdatedAtUtc.Should().Be(initialUpdatedAtUtc);
    }
}
