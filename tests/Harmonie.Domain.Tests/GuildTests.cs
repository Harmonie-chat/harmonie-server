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

    [Fact]
    public void UpdateIconFile_WithValidValue_ShouldSucceed()
    {
        var guild = Guild.Create(GuildName.Create("Icon Guild").Value!, UserId.New()).Value!;
        var iconFileId = UploadedFileId.New();

        var result = guild.UpdateIconFile(iconFileId);

        result.IsSuccess.Should().BeTrue();
        guild.IconFileId.Should().Be(iconFileId);
    }

    [Fact]
    public void UpdateIconColor_WithNull_ShouldClear()
    {
        var guild = Guild.Create(GuildName.Create("Color Guild").Value!, UserId.New()).Value!;
        guild.UpdateIconColor("#7C3AED");

        var result = guild.UpdateIconColor(null);

        result.IsSuccess.Should().BeTrue();
        guild.IconColor.Should().BeNull();
    }

    [Fact]
    public void UpdateIconName_WithTooLongValue_ShouldFail()
    {
        var guild = Guild.Create(GuildName.Create("Icon Name Guild").Value!, UserId.New()).Value!;

        var result = guild.UpdateIconName(new string('i', 51));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Guild icon name is too long");
    }

    [Fact]
    public void UpdateIconBg_WithTooLongValue_ShouldFail()
    {
        var guild = Guild.Create(GuildName.Create("Background Guild").Value!, UserId.New()).Value!;

        var result = guild.UpdateIconBg(new string('b', 51));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Guild icon background is too long");
    }
}
