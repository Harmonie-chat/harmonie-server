using FluentAssertions;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class ListUserGuildsHandlerTests
{
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly ListUserGuildsHandler _handler;

    public ListUserGuildsHandlerTests()
    {
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _handler = new ListUserGuildsHandler(
            _guildMemberRepositoryMock.Object,
            NullLogger<ListUserGuildsHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasNoGuilds_ShouldReturnEmptyCollection()
    {
        var userId = UserId.New();

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _handler.HandleAsync(userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Guilds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasGuildMemberships_ShouldReturnMappedGuilds()
    {
        var userId = UserId.New();
        var guildOne = CreateMembership("Guild Alpha", GuildRole.Admin, DateTime.UtcNow.AddDays(-2));
        var guildTwo = CreateMembership("Guild Beta", GuildRole.Member, DateTime.UtcNow.AddDays(-1));

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([guildOne, guildTwo]);

        var response = await _handler.HandleAsync(userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Guilds.Should().HaveCount(2);
        response.Data.Guilds[0].Name.Should().Be("Guild Alpha");
        response.Data.Guilds[0].Role.Should().Be("Admin");
        response.Data.Guilds[1].Name.Should().Be("Guild Beta");
        response.Data.Guilds[1].Role.Should().Be("Member");
    }

    private static UserGuildMembership CreateMembership(
        string guildName,
        GuildRole role,
        DateTime joinedAtUtc)
    {
        var guildNameResult = GuildName.Create(guildName);
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guild = Guild.Rehydrate(
            GuildId.New(),
            guildNameResult.Value,
            UserId.New(),
            createdAtUtc: joinedAtUtc.AddHours(-1),
            updatedAtUtc: joinedAtUtc.AddHours(-1));

        return new UserGuildMembership(guild, role, joinedAtUtc);
    }
}
