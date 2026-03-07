using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class JoinVoiceChannelHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILiveKitTokenService> _liveKitTokenServiceMock;
    private readonly JoinVoiceChannelHandler _handler;

    public JoinVoiceChannelHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _liveKitTokenServiceMock = new Mock<ILiveKitTokenService>();

        _handler = new JoinVoiceChannelHandler(
            _guildChannelRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _userRepositoryMock.Object,
            _liveKitTokenServiceMock.Object,
            NullLogger<JoinVoiceChannelHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var channelId = GuildChannelId.New();
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildChannel?)null);

        var response = await _handler.HandleAsync(channelId, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsText_ShouldReturnNotVoice()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var response = await _handler.HandleAsync(channel.Id, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotVoice);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotGuildMember_ShouldReturnAccessDenied()
    {
        var channel = CreateChannel(GuildChannelType.Voice);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(channel.Id, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnUserNotFound()
    {
        var channel = CreateChannel(GuildChannelType.Voice);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await _handler.HandleAsync(channel.Id, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestIsValid_ShouldReturnLiveKitConnectionInfo()
    {
        var channel = CreateChannel(GuildChannelType.Voice);
        var user = CreateUser();
        var roomToken = new LiveKitRoomToken(
            Token: "eyJ.token",
            Url: "ws://localhost:7880",
            RoomName: $"channel:{channel.Id}");

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _liveKitTokenServiceMock
            .Setup(x => x.GenerateRoomTokenAsync(
                channel.Id,
                user.Id,
                user.Username.Value,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(roomToken);

        var response = await _handler.HandleAsync(channel.Id, user.Id);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Token.Should().Be(roomToken.Token);
        response.Data.Url.Should().Be(roomToken.Url);
        response.Data.RoomName.Should().Be(roomToken.RoomName);
    }

    private static GuildChannel CreateChannel(GuildChannelType type)
    {
        var channelResult = GuildChannel.Create(
            GuildId.New(),
            "voice-room",
            type,
            isDefault: false,
            position: 1);
        if (channelResult.IsFailure || channelResult.Value is null)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return channelResult.Value;
    }

    private static User CreateUser()
    {
        var emailResult = Email.Create($"test-{Guid.NewGuid():N}@harmonie.chat");
        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Failed to create email for tests.");

        var usernameResult = Username.Create($"user{Guid.NewGuid():N}"[..20]);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create username for tests.");

        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            "hashed_password");
        if (userResult.IsFailure || userResult.Value is null)
            throw new InvalidOperationException("Failed to create user for tests.");

        return userResult.Value;
    }
}
