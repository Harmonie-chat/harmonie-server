using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.RemoveMember;
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

public sealed class RemoveMemberHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly RemoveMemberHandler _handler;

    public RemoveMemberHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildNotifierMock = new Mock<IGuildNotifier>();

        _handler = new RemoveMemberHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            _guildNotifierMock.Object,
            NullLogger<RemoveMemberHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(new RemoveMemberInput(guildId, targetId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(new RemoveMemberInput(guild.Id, targetId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new RemoveMemberInput(guild.Id, targetId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetIsNotMember_ShouldReturnMemberNotFound()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserWithRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildMemberUserRole?)null);

        var response = await _handler.HandleAsync(new RemoveMemberInput(guild.Id, targetId), ownerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetIsOwner_ShouldReturnOwnerCannotBeRemoved()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserWithRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildMemberUserRole(GuildRole.Admin, "owner", null));

        var response = await _handler.HandleAsync(new RemoveMemberInput(guild.Id, ownerId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminRemovesRegularMember_ShouldSucceedAndCallRemoveAsync()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserWithRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildMemberUserRole(GuildRole.Member, "targetuser", null));

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new RemoveMemberInput(guild.Id, targetId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminRemovesRegularMember_ShouldCallNotifyMemberRemovedAsync()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserWithRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildMemberUserRole(GuildRole.Member, "targetuser", null));

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _guildNotifierMock
            .Setup(x => x.NotifyMemberRemovedAsync(It.IsAny<MemberRemovedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new RemoveMemberInput(guild.Id, targetId), callerId, TestContext.Current.CancellationToken);

        _guildNotifierMock.Verify(
            x => x.NotifyMemberRemovedAsync(
                It.Is<MemberRemovedNotification>(n => n.GuildId == guild.Id && n.RemovedUserId == targetId && n.Username == "targetuser"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
