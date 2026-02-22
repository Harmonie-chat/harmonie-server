using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class SendMessageHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IChannelMessageRepository> _channelMessageRepositoryMock;
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _channelMessageRepositoryMock = new Mock<IChannelMessageRepository>();

        _handler = new SendMessageHandler(
            _guildChannelRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _channelMessageRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var channelId = GuildChannelId.New();
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildChannel?)null);

        var response = await _handler.HandleAsync(channelId, request, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        if (response.Error is null)
            throw new InvalidOperationException("Expected channel not found error.");

        response.Error.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnNotText()
    {
        var channel = CreateChannel(GuildChannelType.Voice);
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        if (response.Error is null)
            throw new InvalidOperationException("Expected channel not text error.");

        response.Error.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnAccessDenied()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        if (response.Error is null)
            throw new InvalidOperationException("Expected channel access denied error.");

        response.Error.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnMessageContentEmpty()
    {
        var response = await _handler.HandleAsync(
            GuildChannelId.New(),
            new SendMessageRequest("   "),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        if (response.Error is null)
            throw new InvalidOperationException("Expected message content empty error.");

        response.Error.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldPersistTrimmedContent()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var request = new SendMessageRequest("  hello team  ");

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ChannelMessage? persistedMessage = null;
        _channelMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<ChannelMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChannelMessage, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        if (response.Data is null)
            throw new InvalidOperationException("Expected send message payload.");

        response.Data.Content.Should().Be("hello team");
        persistedMessage.Should().NotBeNull();
        if (persistedMessage is null)
            throw new InvalidOperationException("Expected persisted message callback.");

        persistedMessage.Content.Value.Should().Be("hello team");
    }

    private static GuildChannel CreateChannel(GuildChannelType type)
    {
        var channelResult = GuildChannel.Create(
            GuildId.New(),
            "general",
            type,
            isDefault: true,
            position: 0);
        if (channelResult.IsFailure || channelResult.Value is null)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return channelResult.Value;
    }
}
