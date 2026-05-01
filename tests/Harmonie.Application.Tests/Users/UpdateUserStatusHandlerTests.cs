using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.UpdateUserStatus;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Users;

public sealed class UpdateUserStatusHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IUserPresenceNotifier> _userPresenceNotifierMock;
    private readonly UpdateUserStatusHandler _handler;

    public UpdateUserStatusHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _userPresenceNotifierMock = new Mock<IUserPresenceNotifier>();
        _handler = new UpdateUserStatusHandler(
            _userRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _userPresenceNotifierMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidStatus_ShouldUpdateAndReturnStatus()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateUserStatusRequest("dnd");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGuildMembership>());

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.UserId.Should().Be(user.Id.Value);
        response.Data.Status.Should().Be("dnd");

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();
        var request = new UpdateUserStatusRequest("online");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await _handler.HandleAsync(request, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidStatus_ShouldReturnValidationFailure()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateUserStatusRequest("away");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        response.Error.Errors.Should().NotBeNull();
        response.Error.Errors!.Should().ContainKey(nameof(request.Status));

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvisible_ShouldBroadcastOfflineToGuilds()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateUserStatusRequest("invisible");
        var guildId = GuildId.New();
        var guild = ApplicationTestBuilders.CreateGuild(user.Id, guildId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserGuildMembership(guild, Domain.Enums.GuildRole.Member, DateTime.UtcNow)
            });

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Status.Should().Be("invisible");

        _userPresenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.UserId == user.Id &&
                    n.Status == "offline" &&
                    n.GuildIds.Count == 1 &&
                    n.GuildIds[0] == guildId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithOnline_ShouldBroadcastOnlineToGuilds()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateUserStatusRequest("online");
        var guildId = GuildId.New();
        var guild = ApplicationTestBuilders.CreateGuild(user.Id, guildId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserGuildMembership(guild, Domain.Enums.GuildRole.Member, DateTime.UtcNow)
            });

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();

        _userPresenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.Status == "online"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasNoGuilds_ShouldNotBroadcast()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateUserStatusRequest("idle");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGuildMembership>());

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();

        _userPresenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.IsAny<UserPresenceChangedNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
