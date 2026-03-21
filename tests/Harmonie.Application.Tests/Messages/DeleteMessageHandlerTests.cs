using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.DeleteMessage;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class DeleteMessageHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IMessageRepository> _channelMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ITextChannelNotifier> _textChannelNotifierMock;
    private readonly DeleteMessageHandler _handler;

    public DeleteMessageHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _channelMessageRepositoryMock = new Mock<IMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _textChannelNotifierMock = new Mock<ITextChannelNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageDeletedAsync(It.IsAny<TextChannelMessageDeletedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new DeleteMessageHandler(
            _guildChannelRepositoryMock.Object,
            _channelMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _textChannelNotifierMock.Object,
            NullLogger<DeleteMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnChannelNotFound()
    {
        var channelId = GuildChannelId.New();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var response = await _handler.HandleAsync(channelId, MessageId.New(), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnChannelNotText()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var response = await _handler.HandleAsync(channel.Id, MessageId.New(), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnChannelAccessDenied()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var response = await _handler.HandleAsync(channel.Id, MessageId.New(), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageDoesNotExist_ShouldReturnMessageNotFound()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var messageId = MessageId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var response = await _handler.HandleAsync(channel.Id, messageId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageBelongsToAnotherChannel_ShouldReturnMessageNotFound()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var messageId = MessageId.New();
        var messageFromOtherChannel = ApplicationTestBuilders.CreateChannelMessage(GuildChannelId.New(), callerId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOtherChannel);

        var response = await _handler.HandleAsync(channel.Id, messageId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMemberTriesToDeleteAnotherUsersMessage_ShouldReturnDeleteForbidden()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(channel.Id, messageId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesOwnMessage_ShouldReturnSuccess()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(channel.Id, messageId, authorId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesOwnMessage_ShouldPersistAndCommit()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(channel.Id, messageId, authorId);

        _channelMessageRepositoryMock.Verify(
            x => x.SoftDeleteAsync(
                It.Is<Message>(m =>
                    m.Id == message.Id
                    && m.DeletedAtUtc != null
                    && m.UpdatedAtUtc != null),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminDeletesAnotherUsersMessage_ShouldReturnSuccess()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var adminId = UserId.New();
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(channel.Id, messageId, adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAdminDeletesAnotherUsersMessage_ShouldPersistAndCommit()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var adminId = UserId.New();
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(channel.Id, messageId, adminId);

        _channelMessageRepositoryMock.Verify(
            x => x.SoftDeleteAsync(
                It.Is<Message>(m =>
                    m.Id == message.Id
                    && m.DeletedAtUtc != null
                    && m.UpdatedAtUtc != null),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesOwnMessage_ShouldNotifyMessageDeleted()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(channel.Id, messageId, authorId);

        response.Success.Should().BeTrue();
        _textChannelNotifierMock.Verify(
            x => x.NotifyMessageDeletedAsync(
                It.Is<TextChannelMessageDeletedNotification>(n =>
                    n.MessageId == messageId &&
                    n.ChannelId == channel.Id &&
                    n.GuildId == channel.GuildId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId);

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageDeletedAsync(It.IsAny<TextChannelMessageDeletedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(channel.Id, messageId, authorId);

        response.Success.Should().BeTrue();
    }
}
