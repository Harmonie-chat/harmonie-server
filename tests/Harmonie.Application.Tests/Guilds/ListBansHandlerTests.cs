using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.ListBans;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Guilds;

public sealed class ListBansHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildBanRepository> _guildBanRepositoryMock;
    private readonly ListBansHandler _handler;

    public ListBansHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildBanRepositoryMock = new Mock<IGuildBanRepository>();

        _handler = new ListBansHandler(
            _guildRepositoryMock.Object,
            _guildBanRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminAndNoBans_ShouldReturnEmptyList()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildBanRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuildBanWithUser>());

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.Bans.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenAdminAndBansExist_ShouldReturnBanList()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var bannedUserId = UserId.New();
        var bannedUsername = Username.Create("banneduser")!.Value!;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var bans = new[]
        {
            new GuildBanWithUser(
                bannedUserId,
                bannedUsername,
                "Banned User",
                null,
                "#ff0000",
                null,
                null,
                "Spamming",
                ownerId,
                DateTime.UtcNow)
        };

        _guildBanRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bans);

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Bans.Should().HaveCount(1);

        var ban = response.Data.Bans[0];
        ban.UserId.Should().Be(bannedUserId.ToString());
        ban.Username.Should().Be("banneduser");
        ban.DisplayName.Should().Be("Banned User");
        ban.Reason.Should().Be("Spamming");
        ban.BannedBy.Should().Be(ownerId.ToString());
        ban.Avatar.Should().NotBeNull();
        ban.Avatar!.Color.Should().Be("#ff0000");
    }

}
