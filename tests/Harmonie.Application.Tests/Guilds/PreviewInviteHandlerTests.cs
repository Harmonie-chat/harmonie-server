using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.PreviewInvite;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Guilds;

public sealed class PreviewInviteHandlerTests
{
    private readonly Mock<IGuildInviteRepository> _guildInviteRepositoryMock;
    private readonly PreviewInviteHandler _handler;

    public PreviewInviteHandlerTests()
    {
        _guildInviteRepositoryMock = new Mock<IGuildInviteRepository>();

        _handler = new PreviewInviteHandler(
            _guildInviteRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenInviteNotFound_ShouldReturnNotFound()
    {
        _guildInviteRepositoryMock
            .Setup(x => x.GetPreviewByCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitePreview?)null);

        var response = await _handler.HandleAsync("ABCD1234");

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Invite.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenInviteExpired_ShouldReturnExpired()
    {
        var preview = new InvitePreview(
            Code: "ABCD1234",
            GuildName: "Test Guild",
            GuildIconFileId: null,
            GuildIconColor: null,
            GuildIconName: null,
            GuildIconBg: null,
            MemberCount: 5,
            UsesCount: 0,
            MaxUses: null,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(-1));

        _guildInviteRepositoryMock
            .Setup(x => x.GetPreviewByCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var response = await _handler.HandleAsync("ABCD1234");

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Invite.Expired);
    }

    [Fact]
    public async Task HandleAsync_WhenInviteExhausted_ShouldReturnExhausted()
    {
        var preview = new InvitePreview(
            Code: "ABCD1234",
            GuildName: "Test Guild",
            GuildIconFileId: null,
            GuildIconColor: null,
            GuildIconName: null,
            GuildIconBg: null,
            MemberCount: 5,
            UsesCount: 10,
            MaxUses: 10,
            ExpiresAtUtc: null);

        _guildInviteRepositoryMock
            .Setup(x => x.GetPreviewByCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var response = await _handler.HandleAsync("ABCD1234");

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Invite.Exhausted);
    }

    [Fact]
    public async Task HandleAsync_WithValidInvite_ShouldReturnPreview()
    {
        var iconFileId = UploadedFileId.New();
        var preview = new InvitePreview(
            Code: "ABCD1234",
            GuildName: "My Guild",
            GuildIconFileId: iconFileId,
            GuildIconColor: "#FF0000",
            GuildIconName: "sword",
            GuildIconBg: "#000000",
            MemberCount: 42,
            UsesCount: 3,
            MaxUses: 10,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(24));

        _guildInviteRepositoryMock
            .Setup(x => x.GetPreviewByCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var response = await _handler.HandleAsync("ABCD1234");

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.GuildName.Should().Be("My Guild");
        response.Data.GuildIconFileId.Should().Be(iconFileId.ToString());
        response.Data.GuildIcon.Should().NotBeNull();
        response.Data.GuildIcon!.Color.Should().Be("#FF0000");
        response.Data.GuildIcon.Name.Should().Be("sword");
        response.Data.GuildIcon.Bg.Should().Be("#000000");
        response.Data.MemberCount.Should().Be(42);
        response.Data.UsesCount.Should().Be(3);
        response.Data.MaxUses.Should().Be(10);
        response.Data.ExpiresAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithNoIcon_ShouldReturnNullIcon()
    {
        var preview = new InvitePreview(
            Code: "ABCD1234",
            GuildName: "Plain Guild",
            GuildIconFileId: null,
            GuildIconColor: null,
            GuildIconName: null,
            GuildIconBg: null,
            MemberCount: 1,
            UsesCount: 0,
            MaxUses: null,
            ExpiresAtUtc: null);

        _guildInviteRepositoryMock
            .Setup(x => x.GetPreviewByCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var response = await _handler.HandleAsync("ABCD1234");

        response.Success.Should().BeTrue();
        response.Data!.GuildIconFileId.Should().BeNull();
        response.Data.GuildIcon.Should().BeNull();
        response.Data.MaxUses.Should().BeNull();
        response.Data.ExpiresAtUtc.Should().BeNull();
    }
}
