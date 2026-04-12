using System.Runtime.InteropServices.JavaScript;
using FluentAssertions;
using Harmonie.API.RealTime;
using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRRealtimeGroupManagerTests
{
    private readonly Mock<IHubContext<RealtimeHub, IRealtimeClient>> _hubContextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly Mock<IConnectionTracker> _connectionTrackerMock;
    private readonly Mock<IUserSubscriptionRepository> _userSubscriptionRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly SignalRRealtimeGroupManager _manager;

    public SignalRRealtimeGroupManagerTests()
    {
        _hubContextMock = new Mock<IHubContext<RealtimeHub, IRealtimeClient>>();
        _groupManagerMock = new Mock<IGroupManager>();
        _connectionTrackerMock = new Mock<IConnectionTracker>();
        _userSubscriptionRepositoryMock = new Mock<IUserSubscriptionRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();

        _hubContextMock.Setup(x => x.Groups).Returns(_groupManagerMock.Object);

        _manager = new SignalRRealtimeGroupManager(
            _hubContextMock.Object,
            _connectionTrackerMock.Object,
            _userSubscriptionRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            _guildMemberRepositoryMock.Object);
    }

    [Fact]
    public async Task SubscribeConnectionAsync_ShouldAddToAllGroups()
    {
        var userId = UserId.New();
        var guildId = GuildId.New();
        var channelId = GuildChannelId.New();
        var conversationId = ConversationId.New();
        var connectionId = "conn-1";

        _userSubscriptionRepositoryMock
            .Setup(x => x.GetAllAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscriptions(
                new[] { guildId },
                new[] { channelId },
                new[] { conversationId }));

        await _manager.SubscribeConnectionAsync(userId, connectionId);

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(connectionId, $"guild-voice:{guildId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(connectionId, $"channel:{channelId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(connectionId, $"conversation:{conversationId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeConnectionAsync_WithNoSubscriptions_ShouldNotAddToAnyGroup()
    {
        var userId = UserId.New();

        _userSubscriptionRepositoryMock
            .Setup(x => x.GetAllAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscriptions(
                Array.Empty<GuildId>(),
                Array.Empty<GuildChannelId>(),
                Array.Empty<ConversationId>()));

        await _manager.SubscribeConnectionAsync(userId, "conn-1");

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserToGuildGroupsAsync_WhenUserOnline_ShouldAddToGuildAndTextChannels()
    {
        var userId = UserId.New();
        var guildId = GuildId.New();
        var textChannelId = GuildChannelId.New();
        var voiceChannelId = GuildChannelId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(new[] { "conn-1" });

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateChannel(textChannelId, guildId, GuildChannelType.Text),
                CreateChannel(voiceChannelId, guildId, GuildChannelType.Voice)
            });

        await _manager.AddUserToGuildGroupsAsync(userId, guildId);

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-1", $"guild-voice:{guildId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-1", $"channel:{textChannelId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-1", $"channel:{voiceChannelId}", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserToGuildGroupsAsync_WhenUserOffline_ShouldSkip()
    {
        var userId = UserId.New();
        var guildId = GuildId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(Array.Empty<string>());

        await _manager.AddUserToGuildGroupsAsync(userId, guildId);

        _guildChannelRepositoryMock.Verify(
            x => x.GetByGuildIdAsync(It.IsAny<GuildId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveUserFromGuildGroupsAsync_ShouldRemoveFromGuildAndTextChannels()
    {
        var userId = UserId.New();
        var guildId = GuildId.New();
        var textChannelId = GuildChannelId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(new[] { "conn-1" });

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateChannel(textChannelId, guildId, GuildChannelType.Text)
            });

        await _manager.RemoveUserFromGuildGroupsAsync(userId, guildId);

        _groupManagerMock.Verify(
            x => x.RemoveFromGroupAsync("conn-1", $"guild-voice:{guildId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.RemoveFromGroupAsync("conn-1", $"channel:{textChannelId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromGuildGroupsAsync_WhenUserOffline_ShouldSkip()
    {
        var userId = UserId.New();
        var guildId = GuildId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(Array.Empty<string>());

        await _manager.RemoveUserFromGuildGroupsAsync(userId, guildId);

        _groupManagerMock.Verify(
            x => x.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserToChannelGroupAsync_ShouldAddAllConnections()
    {
        var userId = UserId.New();
        var channelId = GuildChannelId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(new[] { "conn-1", "conn-2" });

        await _manager.AddUserToChannelGroupAsync(userId, channelId);

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-1", $"channel:{channelId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-2", $"channel:{channelId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddAllGuildMembersToChannelGroupAsync_ShouldAddOnlineMembersOnly()
    {
        var guildId = GuildId.New();
        var channelId = GuildChannelId.New();
        var onlineUserId = UserId.New();
        var offlineUserId = UserId.New();

        _guildMemberRepositoryMock
            .Setup(x => x.GetGuildMembersAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateMemberUser(onlineUserId),
                CreateMemberUser(offlineUserId)
            });

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(onlineUserId))
            .Returns(new[] { "conn-1" });
        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(offlineUserId))
            .Returns(Array.Empty<string>());

        await _manager.AddAllGuildMembersToChannelGroupAsync(guildId, channelId);

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-1", $"channel:{channelId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddUserToConversationGroupAsync_ShouldAddAllConnections()
    {
        var userId = UserId.New();
        var conversationId = ConversationId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(new[] { "conn-1" });

        await _manager.AddUserToConversationGroupAsync(userId, conversationId);

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync("conn-1", $"conversation:{conversationId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddUserToConversationGroupAsync_WhenUserOffline_ShouldSkip()
    {
        var userId = UserId.New();
        var conversationId = ConversationId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(Array.Empty<string>());

        await _manager.AddUserToConversationGroupAsync(userId, conversationId);

        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserToGuildGroupsAsync_WithMultipleConnections_ShouldAddAllToAllGroups()
    {
        var userId = UserId.New();
        var guildId = GuildId.New();
        var channelId = GuildChannelId.New();

        _connectionTrackerMock
            .Setup(x => x.GetConnectionIds(userId))
            .Returns(new[] { "conn-1", "conn-2" });

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateChannel(channelId, guildId, GuildChannelType.Text)
            });

        await _manager.AddUserToGuildGroupsAsync(userId, guildId);

        // 2 connections x (1 guild + 1 channel) = 4 calls
        _groupManagerMock.Verify(
            x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    private static GuildChannel CreateChannel(GuildChannelId id, GuildId guildId, GuildChannelType type)
    {
        return GuildChannel.Rehydrate(id, guildId, type == GuildChannelType.Text ? "text-channel" : "voice-channel", type, false, 0, DateTime.UtcNow);
    }

    private static GuildMemberUser CreateMemberUser(UserId userId)
    {
        return new GuildMemberUser(
            userId,
            Username.Create("user" + Guid.NewGuid().ToString("N")[..12]).Value!,
            DisplayName: null,
            AvatarFileId: null,
            Bio: null,
            AvatarColor: null,
            AvatarIcon: null,
            AvatarBg: null,
            IsActive: true,
            Role: GuildRole.Member,
            JoinedAtUtc: DateTime.UtcNow);
    }
}
