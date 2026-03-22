using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.LeaveGuild;
using Harmonie.Application.Interfaces.Common;
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

public sealed class LeaveGuildHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly LeaveGuildHandler _handler;

    public LeaveGuildHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();

        _handler = new LeaveGuildHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            NullLogger<LeaveGuildHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var userId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(new LeaveGuildInput(guildId), userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnForbidden()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var userId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsOwner_ShouldReturnConflict()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), ownerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotLeave);
    }

    [Fact]
    public async Task HandleAsync_WhenMemberLeaves_ShouldReturnSuccess()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var memberId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, memberId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), memberId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, memberId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminNonOwnerLeaves_ShouldReturnSuccess()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, adminId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
