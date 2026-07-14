using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Voice.HandleLiveKitWebhook;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Voice;

public sealed class HandleLiveKitWebhookHandlerTests
{
    private readonly Mock<ILiveKitWebhookReceiver> _webhookReceiverMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IVoicePresenceNotifier> _voicePresenceNotifierMock;
    private readonly Mock<IVoiceParticipantCache> _voiceParticipantCacheMock;
    private readonly Mock<IConversationVoicePresenceNotifier> _conversationVoicePresenceNotifierMock;
    private readonly Mock<IConversationVoiceParticipantCache> _conversationVoiceParticipantCacheMock;
    private readonly HandleLiveKitWebhookHandler _handler;

    public HandleLiveKitWebhookHandlerTests()
    {
        _webhookReceiverMock = new Mock<ILiveKitWebhookReceiver>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _voicePresenceNotifierMock = new Mock<IVoicePresenceNotifier>();
        _voiceParticipantCacheMock = new Mock<IVoiceParticipantCache>();
        _conversationVoicePresenceNotifierMock = new Mock<IConversationVoicePresenceNotifier>();
        _conversationVoiceParticipantCacheMock = new Mock<IConversationVoiceParticipantCache>();

        _handler = new HandleLiveKitWebhookHandler(
            _webhookReceiverMock.Object,
            _guildChannelRepositoryMock.Object,
            _conversationRepositoryMock.Object,
            _voicePresenceNotifierMock.Object,
            _voiceParticipantCacheMock.Object,
            _conversationVoicePresenceNotifierMock.Object,
            _conversationVoiceParticipantCacheMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenSignatureIsInvalid_ShouldReturnUnauthorized()
    {
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer invalid");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Fail("invalid signature"));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    [Fact]
    public async Task HandleAsync_WhenEventTypeIsUnsupported_ShouldIgnoreWebhook()
    {
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "room_started",
                    "channel:ignored",
                    null,
                    null,
                    TestClock.UtcNow)));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Processed.Should().BeFalse();
        response.Data.EventType.Should().Be("room_started");

        _guildChannelRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<GuildChannelId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _voicePresenceNotifierMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRoomNameDoesNotMatchConvention_ShouldIgnoreWebhook()
    {
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_joined",
                    "guild:not-a-channel",
                    UserId.New().ToString(),
                    "alice",
                    TestClock.UtcNow)));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Processed.Should().BeFalse();
        response.Data.EventType.Should().Be("participant_joined");
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantJoined_ShouldNotifyWithAvatarData()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var avatarFileId = UploadedFileId.New();
        var profile = new ChannelParticipantProfile(
            Username.Create("alice").Value!,
            DisplayName: "Alice",
            AvatarFileId: avatarFileId,
            AvatarColor: "#ff0000",
            AvatarIcon: "star",
            AvatarBg: "#000000");
        var occurredAtUtc = TestClock.UtcNow;
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_joined",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    occurredAtUtc)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, profile, "test-guild"));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("participant_joined");

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantJoinedAsync(
                It.Is<VoiceParticipantJoinedNotification>(notification =>
                    notification.GuildId == channel.GuildId
                    && notification.GuildName == "test-guild"
                    && notification.ChannelId == channel.Id
                    && notification.ChannelName == channel.Name
                    && notification.UserId == participantUserId
                    && notification.Username == profile.Username.Value
                    && notification.DisplayName == profile.DisplayName
                    && notification.AvatarFileId == profile.AvatarFileId
                    && notification.AvatarColor == profile.AvatarColor
                    && notification.AvatarIcon == profile.AvatarIcon
                    && notification.AvatarBg == profile.AvatarBg
                    && notification.JoinedAtUtc == occurredAtUtc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantJoinedAndNotGuildMember_ShouldNotifyWithNullAvatarData()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var occurredAtUtc = TestClock.UtcNow;
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_joined",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "ghost",
                    occurredAtUtc)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantJoinedAsync(
                It.Is<VoiceParticipantJoinedNotification>(notification =>
                    notification.GuildName == "test-guild"
                    && notification.ChannelName == channel.Name
                    && notification.UserId == participantUserId
                    && notification.Username == null
                    && notification.DisplayName == null
                    && notification.AvatarFileId == null
                    && notification.AvatarColor == null
                    && notification.AvatarIcon == null
                    && notification.AvatarBg == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantLeft_ShouldNotifyGuildGroup()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var occurredAtUtc = TestClock.UtcNow;
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_left",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    occurredAtUtc)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("participant_left");

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantLeftAsync(
                It.Is<VoiceParticipantLeftNotification>(notification =>
                    notification.GuildId == channel.GuildId
                    && notification.GuildName == "test-guild"
                    && notification.ChannelId == channel.Id
                    && notification.ChannelName == channel.Name
                    && notification.UserId == participantUserId
                    && notification.Username == null
                    && notification.LeftAtUtc == occurredAtUtc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantJoined_ShouldAddToCache()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var avatarFileId = UploadedFileId.New();
        var profile = new ChannelParticipantProfile(
            Username.Create("alice").Value!,
            DisplayName: "Alice",
            AvatarFileId: avatarFileId,
            AvatarColor: "#ff0000",
            AvatarIcon: "star",
            AvatarBg: "#000000");
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_joined",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, profile, "test-guild"));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _voiceParticipantCacheMock.Verify(
            x => x.AddOrUpdateAsync(
                channel.Id,
                It.Is<CachedVoiceParticipant>(p =>
                    p.UserId == participantUserId
                    && p.Username == profile.Username.Value
                    && p.DisplayName == profile.DisplayName
                    && p.AvatarFileId == profile.AvatarFileId
                    && p.AvatarColor == profile.AvatarColor
                    && p.AvatarIcon == profile.AvatarIcon
                    && p.AvatarBg == profile.AvatarBg),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTrackPublishedAndSourceIsScreenShare_ShouldAddTrackAndNotifyFirstTime()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var occurredAtUtc = TestClock.UtcNow;
        var trackSid = "TR_screen123";
        var request = new HandleLiveKitWebhookRequest("{\"event\":\"track_published\"}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_published",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    occurredAtUtc,
                    new LiveKitTrackInfo(trackSid, "SCREEN_SHARE", "VIDEO", false, 1920, 1080))));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        _voiceParticipantCacheMock
            .Setup(x => x.TryAddScreenShareTrackAsync(
                channel.Id, participantUserId, trackSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreenShareTrackAddResult(true));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("track_published");

        _voiceParticipantCacheMock.Verify(
            x => x.TryAddScreenShareTrackAsync(channel.Id, participantUserId, trackSid, It.IsAny<CancellationToken>()),
            Times.Once);

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyScreenShareStartedAsync(
                It.Is<VoiceScreenShareNotification>(n =>
                    n.GuildId == channel.GuildId
                    && n.GuildName == "test-guild"
                    && n.ChannelId == channel.Id
                    && n.ChannelName == channel.Name
                    && n.UserId == participantUserId
                    && n.TimestampUtc == occurredAtUtc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTrackPublishedButNotFirstScreenShare_ShouldNotNotify()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var trackSid = "TR_screen456";
        var request = new HandleLiveKitWebhookRequest("{\"event\":\"track_published\"}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_published",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow,
                    new LiveKitTrackInfo(trackSid, "SCREEN_SHARE", "VIDEO", false, 1920, 1080))));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        _voiceParticipantCacheMock
            .Setup(x => x.TryAddScreenShareTrackAsync(
                channel.Id, participantUserId, trackSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreenShareTrackAddResult(false));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyScreenShareStartedAsync(It.IsAny<VoiceScreenShareNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTrackPublishedAndSourceIsNotScreenShare_ShouldIgnore()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest("{\"event\":\"track_published\"}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_published",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow,
                    new LiveKitTrackInfo("TR_cam123", "CAMERA", "VIDEO", false, 1280, 720))));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("track_published");

        _voiceParticipantCacheMock.Verify(
            x => x.TryAddScreenShareTrackAsync(
                It.IsAny<GuildChannelId>(), It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTrackUnpublishedAndLastScreenShare_ShouldNotifyStopped()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var occurredAtUtc = TestClock.UtcNow;
        var trackSid = "TR_screen789";
        var request = new HandleLiveKitWebhookRequest("{\"event\":\"track_unpublished\"}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_unpublished",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    occurredAtUtc,
                    new LiveKitTrackInfo(trackSid, "SCREEN_SHARE", "VIDEO", false, 1920, 1080))));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        _voiceParticipantCacheMock
            .Setup(x => x.TryRemoveScreenShareTrackAsync(
                channel.Id, participantUserId, trackSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreenShareTrackRemoveResult(true));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("track_unpublished");

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyScreenShareStoppedAsync(
                It.Is<VoiceScreenShareNotification>(n =>
                    n.GuildId == channel.GuildId
                    && n.GuildName == "test-guild"
                    && n.ChannelId == channel.Id
                    && n.ChannelName == channel.Name
                    && n.UserId == participantUserId
                    && n.TimestampUtc == occurredAtUtc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTrackUnpublishedButNotLastScreenShare_ShouldNotNotify()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var trackSid = "TR_screen_001";
        var request = new HandleLiveKitWebhookRequest("{\"event\":\"track_unpublished\"}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_unpublished",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow,
                    new LiveKitTrackInfo(trackSid, "SCREEN_SHARE", "VIDEO", false, 1920, 1080))));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        _voiceParticipantCacheMock
            .Setup(x => x.TryRemoveScreenShareTrackAsync(
                channel.Id, participantUserId, trackSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreenShareTrackRemoveResult(false));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyScreenShareStoppedAsync(It.IsAny<VoiceScreenShareNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantLeft_ShouldClearScreenShareTracks()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest("{\"event\":\"participant_left\"}", "Bearer token");

        _voiceParticipantCacheMock
            .Setup(x => x.ClearScreenShareTracksAsync(
                channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_left",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _voiceParticipantCacheMock.Verify(
            x => x.ClearScreenShareTracksAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantLeft_ShouldRemoveFromCache()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_left",
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _voiceParticipantCacheMock.Verify(
            x => x.RemoveAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("track_published")]
    [InlineData("track_unpublished")]
    public async Task HandleAsync_WhenTrackEventHasNullTrack_ShouldProcessedWithoutCacheOperations(string eventType)
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest($"{{\"event\":\"{eventType}\"}}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    eventType,
                    $"channel:{channel.Id}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow,
                    Track: null)));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(channel.Id, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelWithParticipant(channel, null, "test-guild"));

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be(eventType);

        _voiceParticipantCacheMock.Verify(
            x => x.TryAddScreenShareTrackAsync(
                It.IsAny<GuildChannelId>(), It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _voiceParticipantCacheMock.Verify(
            x => x.TryRemoveScreenShareTrackAsync(
                It.IsAny<GuildChannelId>(), It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Conversation voice webhook tests ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WhenConversationParticipantJoined_ShouldAddToCacheAndNotify()
    {
        var conversationId = ConversationId.New();
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");
        var occurredAt = TestClock.UtcNow;
        var conversation = Harmonie.Domain.Entities.Conversations.Conversation.Rehydrate(
            conversationId,
            Harmonie.Domain.Entities.Conversations.ConversationType.Direct,
            null,
            TestClock.UtcNow);

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_joined",
                    $"conversation:{conversationId}",
                    participantUserId.ToString(),
                    "alice",
                    occurredAt)));

        _conversationRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationWithParticipant(conversation, null));

        _conversationVoiceParticipantCacheMock
            .Setup(x => x.AddOrUpdateAsync(conversationId, It.IsAny<CachedVoiceParticipant>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();

        _conversationVoiceParticipantCacheMock.Verify(
            x => x.AddOrUpdateAsync(conversationId, It.IsAny<CachedVoiceParticipant>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _conversationVoicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantJoinedAsync(
                It.Is<ConversationVoiceParticipantJoinedNotification>(n =>
                    n.ConversationId == conversationId
                    && n.UserId == participantUserId
                    && n.JoinedAtUtc == occurredAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationParticipantLeft_ShouldClearTracksRemoveAndNotify()
    {
        var conversationId = ConversationId.New();
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");
        var occurredAt = TestClock.UtcNow;
        var conversation = Harmonie.Domain.Entities.Conversations.Conversation.Rehydrate(
            conversationId,
            Harmonie.Domain.Entities.Conversations.ConversationType.Direct,
            null,
            TestClock.UtcNow);

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_left",
                    $"conversation:{conversationId}",
                    participantUserId.ToString(),
                    "alice",
                    occurredAt)));

        _conversationRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationWithParticipant(conversation, null));

        _conversationVoiceParticipantCacheMock
            .Setup(x => x.ClearScreenShareTracksAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeTrue();

        _conversationVoiceParticipantCacheMock.Verify(
            x => x.ClearScreenShareTracksAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()),
            Times.Once);

        _conversationVoiceParticipantCacheMock.Verify(
            x => x.RemoveAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()),
            Times.Once);

        _conversationVoicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantLeftAsync(
                It.Is<ConversationVoiceParticipantLeftNotification>(n =>
                    n.ConversationId == conversationId
                    && n.UserId == participantUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationScreenShareStarted_ShouldNotifyScreenShareStarted()
    {
        var conversationId = ConversationId.New();
        var participantUserId = UserId.New();
        var trackSid = "TR_screen_conv_001";
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");
        var conversation = Harmonie.Domain.Entities.Conversations.Conversation.Rehydrate(
            conversationId,
            Harmonie.Domain.Entities.Conversations.ConversationType.Direct,
            null,
            TestClock.UtcNow);

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_published",
                    $"conversation:{conversationId}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow,
                    new LiveKitTrackInfo(trackSid, "SCREEN_SHARE", "VIDEO", false, 1920, 1080))));

        _conversationRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationWithParticipant(conversation, null));

        _conversationVoiceParticipantCacheMock
            .Setup(x => x.TryAddScreenShareTrackAsync(conversationId, participantUserId, trackSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreenShareTrackAddResult(true));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _conversationVoicePresenceNotifierMock.Verify(
            x => x.NotifyScreenShareStartedAsync(
                It.Is<ConversationVoiceScreenShareNotification>(n =>
                    n.ConversationId == conversationId
                    && n.UserId == participantUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationScreenShareStopped_ShouldNotifyScreenShareStopped()
    {
        var conversationId = ConversationId.New();
        var participantUserId = UserId.New();
        var trackSid = "TR_screen_conv_001";
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");
        var conversation = Harmonie.Domain.Entities.Conversations.Conversation.Rehydrate(
            conversationId,
            Harmonie.Domain.Entities.Conversations.ConversationType.Direct,
            null,
            TestClock.UtcNow);

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "track_unpublished",
                    $"conversation:{conversationId}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow,
                    new LiveKitTrackInfo(trackSid, "SCREEN_SHARE", "VIDEO", false, 1920, 1080))));

        _conversationRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationWithParticipant(conversation, null));

        _conversationVoiceParticipantCacheMock
            .Setup(x => x.TryRemoveScreenShareTrackAsync(conversationId, participantUserId, trackSid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreenShareTrackRemoveResult(true));

        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        _conversationVoicePresenceNotifierMock.Verify(
            x => x.NotifyScreenShareStoppedAsync(
                It.Is<ConversationVoiceScreenShareNotification>(n =>
                    n.ConversationId == conversationId
                    && n.UserId == participantUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationNotFound_ShouldNotProcess()
    {
        var conversationId = ConversationId.New();
        var participantUserId = UserId.New();
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer token");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Ok(
                new LiveKitWebhookEvent(
                    "participant_joined",
                    $"conversation:{conversationId}",
                    participantUserId.ToString(),
                    "alice",
                    TestClock.UtcNow)));

        _conversationRepositoryMock
            .Setup(x => x.GetWithParticipantAsync(conversationId, participantUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationWithParticipant?)null);

        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Processed.Should().BeFalse();
    }

}
