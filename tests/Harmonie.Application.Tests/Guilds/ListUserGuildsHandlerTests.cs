using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Guilds;

public sealed class ListUserGuildsHandlerTests
{
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly ListUserGuildsHandler _handler;

    public ListUserGuildsHandlerTests()
    {
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _handler = new ListUserGuildsHandler(
            _guildMemberRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasNoGuilds_ShouldReturnEmptyCollection()
    {
        var userId = UserId.New();

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _handler.HandleAsync(Unit.Value, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Guilds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasGuildMemberships_ShouldReturnMappedGuilds()
    {
        var userId = UserId.New();
        var guildOne = CreateMembership(
            "Guild Alpha",
            GuildRole.Admin,
            DateTime.UtcNow.AddDays(-2),
            iconFileId: UploadedFileId.From(Guid.Parse("0be76be9-ae27-4961-a4a5-835e1f77387b")),
            iconColor: "#7C3AED",
            iconName: "sword",
            iconBg: "#1F2937");
        var guildTwo = CreateMembership("Guild Beta", GuildRole.Member, DateTime.UtcNow.AddDays(-1));

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([guildOne, guildTwo]);

        var response = await _handler.HandleAsync(Unit.Value, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Guilds.Should().HaveCount(2);
        response.Data.Guilds[0].Name.Should().Be("Guild Alpha");
        response.Data.Guilds[0].IconFileId.Should().Be("0be76be9-ae27-4961-a4a5-835e1f77387b");
        response.Data.Guilds[0].Icon.Should().NotBeNull();
        response.Data.Guilds[0].Icon!.Name.Should().Be("sword");
        response.Data.Guilds[0].Role.Should().Be("Admin");
        response.Data.Guilds[1].Name.Should().Be("Guild Beta");
        response.Data.Guilds[1].Icon.Should().BeNull();
        response.Data.Guilds[1].Role.Should().Be("Member");
    }

    private static UserGuildMembership CreateMembership(
        string guildName,
        GuildRole role,
        DateTime joinedAtUtc,
        UploadedFileId? iconFileId = null,
        string? iconColor = null,
        string? iconName = null,
        string? iconBg = null)
    {
        var guildNameResult = GuildName.Create(guildName);
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guild = Guild.Rehydrate(
            GuildId.New(),
            guildNameResult.Value,
            UserId.New(),
            createdAtUtc: joinedAtUtc.AddHours(-1),
            updatedAtUtc: joinedAtUtc.AddHours(-1),
            iconFileId: iconFileId,
            iconColor: iconColor,
            iconName: iconName,
            iconBg: iconBg);

        return new UserGuildMembership(guild, role, joinedAtUtc);
    }
}
