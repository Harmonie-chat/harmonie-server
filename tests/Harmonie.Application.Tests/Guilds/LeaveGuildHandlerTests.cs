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
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly LeaveGuildHandler _handler;

    public LeaveGuildHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildNotifierMock = new Mock<IGuildNotifier>();

        _guildNotifierMock
            .Setup(x => x.NotifyMemberLeftAsync(
                It.IsAny<MemberLeftNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new LeaveGuildHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            _guildNotifierMock.Object,
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

        var response = await _handler.HandleAsync(new LeaveGuildInput(guildId), userId, TestContext.Current.CancellationToken);

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

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), userId, TestContext.Current.CancellationToken);

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

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), ownerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotLeave);
    }

    [Fact]
    public async Task HandleAsync_WhenMemberLeaves_ShouldReturnSuccessAndNotifyWithUsername()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var memberId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member, "member", "Member Display"));

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, memberId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), memberId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, memberId, It.IsAny<CancellationToken>()),
            Times.Once);

        _guildNotifierMock.Verify(
            x => x.NotifyMemberLeftAsync(
                It.Is<MemberLeftNotification>(n =>
                    n.GuildId == guild.Id && n.UserId == memberId && n.Username == "member" && n.DisplayName == "Member Display"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminNonOwnerLeaves_ShouldReturnSuccessAndNotifyWithUsername()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin, "admin", "Admin Display"));

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new LeaveGuildInput(guild.Id), adminId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, adminId, It.IsAny<CancellationToken>()),
            Times.Once);

        _guildNotifierMock.Verify(
            x => x.NotifyMemberLeftAsync(
                It.Is<MemberLeftNotification>(n =>
                    n.GuildId == guild.Id && n.UserId == adminId && n.Username == "admin" && n.DisplayName == "Admin Display"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
