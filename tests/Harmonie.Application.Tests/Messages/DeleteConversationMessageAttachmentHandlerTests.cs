using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Conversations.DeleteMessageAttachment;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;


namespace Harmonie.Application.Tests.Messages;

public sealed class DeleteConversationMessageAttachmentHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _conversationMessageRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly DeleteMessageAttachmentHandler _handler;

    public DeleteConversationMessageAttachmentHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _conversationMessageRepositoryMock = new Mock<IMessageRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new DeleteMessageAttachmentHandler(
            _conversationRepositoryMock.Object,
            _conversationMessageRepositoryMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
            _unitOfWorkMock.Object,
            NullLogger<DeleteMessageAttachmentHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();
        var attachmentId = UploadedFileId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(conversationId, messageId, attachmentId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnConversationAccessDenied()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            MessageId.New(),
            UploadedFileId.New(),
            outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnDeleteForbidden()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var attachmentId = UploadedFileId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantTwo, content: "hello", attachments: [new MessageAttachment(attachmentId, "notes.txt", "text/plain", 12)]);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(conversation.Id, message.Id, attachmentId, participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task HandleAsync_WhenAttachmentIsNotOnMessage_ShouldReturnAttachmentNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var otherAttachmentId = UploadedFileId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne, content: "hello", attachments: [new MessageAttachment(otherAttachmentId, "notes.txt", "text/plain", 12)]);
        var missingAttachmentId = UploadedFileId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            conversation.Id,
            message.Id,
            missingAttachmentId,
            participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesAttachment_ShouldRemoveReferenceCommitAndCleanupFile()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var attachmentId = UploadedFileId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne, content: "hello", attachments: [new MessageAttachment(attachmentId, "notes.txt", "text/plain", 12)]);
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(id: attachmentId, uploaderUserId: participantOne, fileName: "notes.txt", contentType: "text/plain", sizeBytes: 12, storageKey: "attachments/file.txt");
        var sequence = new MockSequence();

        _conversationRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationMessageRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _conversationMessageRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.UpdateAsync(
                It.Is<Message>(updatedMessage =>
                    updatedMessage.Id == message.Id
                    && updatedMessage.Attachments.All(attachment => attachment.FileId != attachmentId)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _conversationMessageRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.RemoveAttachmentAsync(message.Id, attachmentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .InSequence(sequence)
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .InSequence(sequence)
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.DeleteAsync(attachmentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(conversation.Id, message.Id, attachmentId, participantOne);

        response.Success.Should().BeTrue();
        message.Attachments.Should().BeEmpty();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _conversationMessageRepositoryMock.Verify(
            x => x.RemoveAttachmentAsync(message.Id, attachmentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
