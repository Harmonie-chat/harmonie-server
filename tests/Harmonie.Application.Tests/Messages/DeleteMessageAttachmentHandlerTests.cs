using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Channels.DeleteMessageAttachment;
using Harmonie.Application.Features.Channels.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class DeleteMessageAttachmentHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly MessageEditDeleteOrchestrator _orchestrator;
    private readonly DeleteMessageAttachmentHandler _handler;

    public DeleteMessageAttachmentHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        var uploadedFileCleanupService = new UploadedFileCleanupService(
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            NullLogger<UploadedFileCleanupService>.Instance);

        _orchestrator = new MessageEditDeleteOrchestrator(
            _messageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            uploadedFileCleanupService,
            TestTime.CreateProvider());

        _handler = new DeleteMessageAttachmentHandler(
            _guildChannelRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            new Mock<IMessageEventPublisher>().Object,
            NullLogger<ChannelMessageEditDeleteScope>.Instance,
            _orchestrator);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnChannelNotFound()
    {
        var channelId = GuildChannelId.New();
        var messageId = MessageId.New();
        var attachmentId = UploadedFileId.New();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var response = await _handler.HandleAsync(new DeleteChannelMessageAttachmentInput(channelId, messageId, attachmentId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnDeleteForbidden()
    {
        var channel = ApplicationTestBuilders.CreateChannel();
        var callerId = UserId.New();
        var authorId = UserId.New();
        var attachmentId = UploadedFileId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(new DeleteChannelMessageAttachmentInput(channel.Id, message.Id, attachmentId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task HandleAsync_WhenAttachmentIsNotOnMessage_ShouldReturnAttachmentNotFound()
    {
        var channel = ApplicationTestBuilders.CreateChannel();
        var authorId = UserId.New();
        var attachmentId = UploadedFileId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _messageAttachmentRepositoryMock
            .Setup(x => x.DeleteAsync(message.Id, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(new DeleteChannelMessageAttachmentInput(channel.Id, message.Id, attachmentId), authorId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesAttachment_ShouldRemoveReferenceCommitAndCleanupFile()
    {
        var channel = ApplicationTestBuilders.CreateChannel();
        var authorId = UserId.New();
        var attachmentId = UploadedFileId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, authorId, content: "hello");
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(id: attachmentId, uploaderUserId: authorId, fileName: "notes.txt", contentType: "text/plain", sizeBytes: 12, storageKey: "attachments/file.txt");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _messageAttachmentRepositoryMock
            .Setup(x => x.DeleteAsync(message.Id, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .Setup(x => x.DeleteAsync(attachmentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new DeleteChannelMessageAttachmentInput(channel.Id, message.Id, attachmentId), authorId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _messageAttachmentRepositoryMock.Verify(
            x => x.DeleteAsync(message.Id, attachmentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
