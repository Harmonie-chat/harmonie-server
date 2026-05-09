using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
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

public sealed class EditMessageHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IMessageRepository> _channelMessageRepositoryMock;
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ITextChannelNotifier> _textChannelNotifierMock;
    private readonly MessageEditDeleteOrchestrator _orchestrator;
    private readonly EditMessageHandler _handler;

    public EditMessageHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _channelMessageRepositoryMock = new Mock<IMessageRepository>();
        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _textChannelNotifierMock = new Mock<ITextChannelNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageUpdatedAsync(It.IsAny<TextChannelMessageUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageAttachmentRepositoryMock
            .Setup(x => x.GetByMessageIdAsync(It.IsAny<MessageId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageAttachment>());

        _channelMessageRepositoryMock
            .Setup(x => x.GetMentionedUserIdsByMessageIdAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Guid>>());

        var uploadedFileCleanupService = new UploadedFileCleanupService(
            new Mock<IUploadedFileRepository>().Object,
            new Mock<IObjectStorageService>().Object,
            NullLogger<UploadedFileCleanupService>.Instance);

        _orchestrator = new MessageEditDeleteOrchestrator(
            _channelMessageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            uploadedFileCleanupService);

        _handler = new EditMessageHandler(
            _guildChannelRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _textChannelNotifierMock.Object,
            NullLogger<ChannelMessageEditDeleteScope>.Instance,
            _orchestrator);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnMessageContentEmpty()
    {
        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(GuildChannelId.New(), MessageId.New(), "   "),
            UserId.New(),
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnChannelNotFound()
    {
        var channelId = GuildChannelId.New();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channelId, MessageId.New(), "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

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

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, MessageId.New(), "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

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

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, MessageId.New(), "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

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

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

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
        var messageFromOtherChannel = ApplicationTestBuilders.CreateChannelMessage(GuildChannelId.New(), callerId, content: "original content");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOtherChannel);

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnEditForbidden()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "original content");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldReturnUpdatedMessage()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "original content");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "  updated content  "),
            authorId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("updated content");
        response.Data.ChannelId.Should().Be(channel.Id.Value);
        response.Data.AuthorUserId.Should().Be(authorId.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldPersistAndCommit()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "original content");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "updated content"),
            authorId,
            TestContext.Current.CancellationToken);

        _channelMessageRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldNotifyMessageUpdated()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "original content");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "updated content"),
            authorId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _textChannelNotifierMock.Verify(
            x => x.NotifyMessageUpdatedAsync(
                It.Is<TextChannelMessageUpdatedNotification>(n =>
                    n.ChannelId == channel.Id &&
                    n.GuildId == channel.GuildId &&
                    n.Content == "updated content"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var authorId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "original content");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageUpdatedAsync(It.IsAny<TextChannelMessageUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            new EditChannelMessageInput(channel.Id, messageId, "updated content"),
            authorId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
    }
}
