using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class EditMessageHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IChannelMessageRepository> _channelMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ITextChannelNotifier> _textChannelNotifierMock;
    private readonly EditMessageHandler _handler;

    public EditMessageHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _channelMessageRepositoryMock = new Mock<IChannelMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _textChannelNotifierMock = new Mock<ITextChannelNotifier>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageUpdatedAsync(It.IsAny<TextChannelMessageUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new EditMessageHandler(
            _guildChannelRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _channelMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _textChannelNotifierMock.Object,
            NullLogger<EditMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnMessageContentEmpty()
    {
        var response = await _handler.HandleAsync(
            GuildChannelId.New(),
            ChannelMessageId.New(),
            new EditMessageRequest("   "),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnChannelNotFound()
    {
        var channelId = GuildChannelId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildChannel?)null);

        var response = await _handler.HandleAsync(
            channelId,
            ChannelMessageId.New(),
            new EditMessageRequest("updated content"),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnChannelNotText()
    {
        var channel = CreateChannel(GuildChannelType.Voice);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        var response = await _handler.HandleAsync(
            channel.Id,
            ChannelMessageId.New(),
            new EditMessageRequest("updated content"),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnChannelAccessDenied()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(
            channel.Id,
            ChannelMessageId.New(),
            new EditMessageRequest("updated content"),
            callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageDoesNotExist_ShouldReturnMessageNotFound()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var messageId = ChannelMessageId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelMessage?)null);

        var response = await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("updated content"),
            callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageBelongsToAnotherChannel_ShouldReturnMessageNotFound()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var messageId = ChannelMessageId.New();
        var messageFromOtherChannel = CreateMessage(GuildChannelId.New(), callerId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOtherChannel);

        var response = await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("updated content"),
            callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnEditForbidden()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var authorId = UserId.New();
        var messageId = ChannelMessageId.New();
        var message = CreateMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("updated content"),
            callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldReturnUpdatedMessage()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = ChannelMessageId.New();
        var message = CreateMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("  updated content  "),
            authorId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("updated content");
        response.Data.ChannelId.Should().Be(channel.Id.ToString());
        response.Data.AuthorUserId.Should().Be(authorId.ToString());
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldPersistAndCommit()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = ChannelMessageId.New();
        var message = CreateMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("updated content"),
            authorId);

        _channelMessageRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<ChannelMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldNotifyMessageUpdated()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = ChannelMessageId.New();
        var message = CreateMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("updated content"),
            authorId);

        response.Success.Should().BeTrue();
        _textChannelNotifierMock.Verify(
            x => x.NotifyMessageUpdatedAsync(
                It.Is<TextChannelMessageUpdatedNotification>(n =>
                    n.ChannelId == channel.Id &&
                    n.Content == "updated content"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = ChannelMessageId.New();
        var message = CreateMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(channel.GuildId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageUpdatedAsync(It.IsAny<TextChannelMessageUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            channel.Id,
            messageId,
            new EditMessageRequest("updated content"),
            authorId);

        response.Success.Should().BeTrue();
    }

    private static GuildChannel CreateChannel(GuildChannelType type)
    {
        var result = GuildChannel.Create(
            GuildId.New(),
            "general",
            type,
            isDefault: false,
            position: 0);

        if (result.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return result.Value!;
    }

    private static ChannelMessage CreateMessage(GuildChannelId channelId, UserId authorId)
    {
        var contentResult = ChannelMessageContent.Create("original content");
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create message content for tests.");

        return ChannelMessage.Rehydrate(
            ChannelMessageId.New(),
            channelId,
            authorId,
            contentResult.Value,
            DateTime.UtcNow,
            updatedAtUtc: null);
    }
}
