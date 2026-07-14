using FluentAssertions;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.ValueObjects.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class GuildTests
{
    [Fact]
    public void UpdateName_WithDifferentName_ShouldUpdateNameAndTimestamp()
    {
        var originalName = GuildName.Create("Original Guild").Value!;
        var updatedName = GuildName.Create("Updated Guild").Value!;
        var guildResult = Guild.Create(originalName, UserId.New(), TestTime.UtcNow);
        guildResult.IsSuccess.Should().BeTrue();
        guildResult.Value.Should().NotBeNull();

        var guild = guildResult.Value!;
        var updateResult = guild.UpdateName(updatedName, TestTime.UtcNow.AddMinutes(1));

        updateResult.IsSuccess.Should().BeTrue();
        guild.Name.Should().Be(updatedName);
        guild.UpdatedAtUtc.Should().Be(TestTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void UpdateName_WithSameName_ShouldSucceedWithoutChangingTimestamp()
    {
        var guildName = GuildName.Create("Stable Guild").Value!;
        var guild = Guild.Create(guildName, UserId.New(), TestTime.UtcNow).Value!;
        var initialUpdatedAtUtc = guild.UpdatedAtUtc;

        var result = guild.UpdateName(guildName, TestTime.UtcNow.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        guild.UpdatedAtUtc.Should().Be(initialUpdatedAtUtc);
    }

    [Fact]
    public void UpdateIconFile_WithValidValue_ShouldSucceed()
    {
        var guild = Guild.Create(GuildName.Create("Icon Guild").Value!, UserId.New(), TestTime.UtcNow).Value!;
        var iconFileId = UploadedFileId.New();

        var result = guild.UpdateIconFile(iconFileId, TestTime.UtcNow.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        guild.IconFileId.Should().Be(iconFileId);
    }

    [Fact]
    public void UpdateIcon_WithValidAppearance_ShouldSucceed()
    {
        var guild = Guild.Create(GuildName.Create("Color Guild").Value!, UserId.New(), TestTime.UtcNow).Value!;
        var appearance = Appearance.Create("#7C3AED", "star", "#FFF").Value!;

        var result = guild.UpdateIcon(appearance, TestTime.UtcNow.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        guild.Icon.Color.Should().Be("#7C3AED");
        guild.Icon.Glyph.Should().Be("star");
        guild.Icon.Bg.Should().Be("#FFF");
    }

    [Fact]
    public void UpdateIcon_WithNullFieldsInAppearance_ShouldClear()
    {
        var guild = Guild.Create(GuildName.Create("Color Guild").Value!, UserId.New(), TestTime.UtcNow).Value!;
        guild.UpdateIcon(Appearance.Create("#7C3AED", null, null).Value!, TestTime.UtcNow.AddMinutes(1));

        var result = guild.UpdateIcon(Appearance.Empty, TestTime.UtcNow.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        guild.Icon.HasValue.Should().BeFalse();
    }
}
