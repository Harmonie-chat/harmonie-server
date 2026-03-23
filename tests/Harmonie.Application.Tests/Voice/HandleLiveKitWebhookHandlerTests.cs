using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Voice.HandleLiveKitWebhook;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Voice;

public sealed class HandleLiveKitWebhookHandlerTests
{
    private readonly Mock<ILiveKitWebhookReceiver> _webhookReceiverMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IVoicePresenceNotifier> _voicePresenceNotifierMock;
    private readonly HandleLiveKitWebhookHandler _handler;

    public HandleLiveKitWebhookHandlerTests()
    {
        _webhookReceiverMock = new Mock<ILiveKitWebhookReceiver>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _voicePresenceNotifierMock = new Mock<IVoicePresenceNotifier>();

        _handler = new HandleLiveKitWebhookHandler(
            _webhookReceiverMock.Object,
            _guildChannelRepositoryMock.Object,
            _voicePresenceNotifierMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenSignatureIsInvalid_ShouldReturnUnauthorized()
    {
        var request = new HandleLiveKitWebhookRequest("{}", "Bearer invalid");

        _webhookReceiverMock
            .Setup(x => x.Receive(request.RawBody, request.AuthorizationHeader!))
            .Returns(LiveKitWebhookReceiveResult.Fail("invalid signature"));

        var response = await _handler.HandleAsync(request);

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
                    DateTime.UtcNow)));

        var response = await _handler.HandleAsync(request);

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
                    DateTime.UtcNow)));

        var response = await _handler.HandleAsync(request);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Processed.Should().BeFalse();
        response.Data.EventType.Should().Be("participant_joined");
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantJoined_ShouldNotifyGuildGroup()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var occurredAtUtc = DateTime.UtcNow;
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
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var response = await _handler.HandleAsync(request);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("participant_joined");

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantJoinedAsync(
                It.Is<VoiceParticipantJoinedNotification>(notification =>
                    notification.GuildId == channel.GuildId
                    && notification.ChannelId == channel.Id
                    && notification.UserId == participantUserId
                    && notification.ParticipantName == "alice"
                    && notification.JoinedAtUtc == occurredAtUtc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantLeft_ShouldNotifyGuildGroup()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var participantUserId = UserId.New();
        var occurredAtUtc = DateTime.UtcNow;
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
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var response = await _handler.HandleAsync(request);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Processed.Should().BeTrue();
        response.Data.EventType.Should().Be("participant_left");

        _voicePresenceNotifierMock.Verify(
            x => x.NotifyParticipantLeftAsync(
                It.Is<VoiceParticipantLeftNotification>(notification =>
                    notification.GuildId == channel.GuildId
                    && notification.ChannelId == channel.Id
                    && notification.UserId == participantUserId
                    && notification.ParticipantName == "alice"
                    && notification.LeftAtUtc == occurredAtUtc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
