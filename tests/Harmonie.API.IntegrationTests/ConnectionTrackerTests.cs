using FluentAssertions;
using Harmonie.API.RealTime;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ConnectionTrackerTests : IDisposable
{
    private static readonly TimeSpan TestGracePeriod = TimeSpan.FromMilliseconds(200);

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IUserPresenceNotifier> _presenceNotifierMock;
    private readonly ConnectionTracker _tracker;

    public ConnectionTrackerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _presenceNotifierMock = new Mock<IUserPresenceNotifier>();

        var services = new ServiceCollection();
        services.AddScoped(_ => _userRepositoryMock.Object);
        services.AddScoped(_ => _guildMemberRepositoryMock.Object);
        services.AddScoped(_ => _presenceNotifierMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _tracker = new ConnectionTracker(
            scopeFactory,
            NullLogger<ConnectionTracker>.Instance,
            TestGracePeriod);
    }

    public void Dispose()
    {
        _tracker.Dispose();
    }

    [Fact]
    public async Task HandleConnectedAsync_FirstConnection_ShouldMarkUserOnline()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGuildMembership>());

        await _tracker.HandleConnectedAsync(userId, "conn-1");

        _tracker.IsOnline(userId).Should().BeTrue();
    }

    [Fact]
    public async Task HandleConnectedAsync_FirstConnection_ShouldBroadcastUserStatus()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);
        var guildId = GuildId.New();
        var guild = CreateGuild(guildId, userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserGuildMembership(guild, Domain.Enums.GuildRole.Member, DateTime.UtcNow)
            });

        await _tracker.HandleConnectedAsync(userId, "conn-1");

        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.UserId == userId &&
                    n.Status == "online" &&
                    n.GuildIds.Count == 1 &&
                    n.GuildIds[0] == guildId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleConnectedAsync_SecondConnection_ShouldNotBroadcastAgain()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGuildMembership>());

        await _tracker.HandleConnectedAsync(userId, "conn-1");
        await _tracker.HandleConnectedAsync(userId, "conn-2");

        _tracker.IsOnline(userId).Should().BeTrue();

        _userRepositoryMock.Verify(
            x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDisconnectedAsync_NotLastConnection_ShouldRemainOnline()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGuildMembership>());

        await _tracker.HandleConnectedAsync(userId, "conn-1");
        await _tracker.HandleConnectedAsync(userId, "conn-2");
        await _tracker.HandleDisconnectedAsync(userId, "conn-1");

        _tracker.IsOnline(userId).Should().BeTrue();
    }

    [Fact]
    public async Task HandleDisconnectedAsync_LastConnection_ShouldBroadcastOfflineAfterGracePeriod()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);
        var guildId = GuildId.New();
        var guild = CreateGuild(guildId, userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserGuildMembership(guild, Domain.Enums.GuildRole.Member, DateTime.UtcNow)
            });

        await _tracker.HandleConnectedAsync(userId, "conn-1");
        _presenceNotifierMock.Invocations.Clear();

        await _tracker.HandleDisconnectedAsync(userId, "conn-1");

        // Should not broadcast immediately
        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.IsAny<UserPresenceChangedNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Wait for grace period to expire
        await Task.Delay(TestGracePeriod + TimeSpan.FromMilliseconds(200));

        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.UserId == userId &&
                    n.Status == "offline"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleConnectedAsync_DuringGracePeriod_ShouldCancelOfflineBroadcast()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);
        var guildId = GuildId.New();
        var guild = CreateGuild(guildId, userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserGuildMembership(guild, Domain.Enums.GuildRole.Member, DateTime.UtcNow)
            });

        await _tracker.HandleConnectedAsync(userId, "conn-1");
        _presenceNotifierMock.Invocations.Clear();

        await _tracker.HandleDisconnectedAsync(userId, "conn-1");

        // Reconnect before grace period expires
        await Task.Delay(TestGracePeriod / 2);
        await _tracker.HandleConnectedAsync(userId, "conn-2");

        // Wait for grace period to fully expire
        await Task.Delay(TestGracePeriod + TimeSpan.FromMilliseconds(200));

        // Should have broadcast online on reconnect, not offline
        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.Status == "offline"),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.Status == "online"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleConnectedAsync_InvisibleUser_ShouldBroadcastOffline()
    {
        var userId = UserId.New();
        var user = CreateUser(userId, "invisible");
        var guildId = GuildId.New();
        var guild = CreateGuild(guildId, userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserGuildMembership(guild, Domain.Enums.GuildRole.Member, DateTime.UtcNow)
            });

        await _tracker.HandleConnectedAsync(userId, "conn-1");

        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.Is<UserPresenceChangedNotification>(n =>
                    n.UserId == userId &&
                    n.Status == "offline"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsOnline_WhenNoConnections_ShouldReturnFalse()
    {
        var userId = UserId.New();
        _tracker.IsOnline(userId).Should().BeFalse();
    }

    [Fact]
    public async Task HandleDisconnectedAsync_UnknownUser_ShouldNotThrow()
    {
        var userId = UserId.New();
        await _tracker.HandleDisconnectedAsync(userId, "conn-1");
    }

    [Fact]
    public async Task HandleConnectedAsync_UserWithNoGuilds_ShouldNotBroadcast()
    {
        var userId = UserId.New();
        var user = CreateUser(userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _guildMemberRepositoryMock
            .Setup(x => x.GetUserGuildMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserGuildMembership>());

        await _tracker.HandleConnectedAsync(userId, "conn-1");

        _presenceNotifierMock.Verify(
            x => x.NotifyStatusChangedAsync(
                It.IsAny<UserPresenceChangedNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static User CreateUser(UserId? userId = null, string status = "online")
    {
        var id = userId ?? UserId.New();
        var emailResult = Email.Create($"test-{Guid.NewGuid():N}@harmonie.chat");
        var usernameResult = Username.Create($"user{Guid.NewGuid():N}"[..20]);

        return User.Rehydrate(
            id,
            emailResult.Value!,
            usernameResult.Value!,
            "hashed_password",
            avatarFileId: null,
            isEmailVerified: true,
            isActive: true,
            lastLoginAtUtc: DateTime.UtcNow,
            displayName: null,
            bio: null,
            avatarColor: null,
            avatarIcon: null,
            avatarBg: null,
            theme: "default",
            language: null,
            status: status,
            statusUpdatedAtUtc: DateTime.UtcNow,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null);
    }

    private static Guild CreateGuild(GuildId guildId, UserId ownerId)
    {
        var nameResult = GuildName.Create($"guild-{Guid.NewGuid():N}"[..20]);
        return Guild.Rehydrate(
            guildId,
            nameResult.Value!,
            ownerId,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: DateTime.UtcNow);
    }
}
