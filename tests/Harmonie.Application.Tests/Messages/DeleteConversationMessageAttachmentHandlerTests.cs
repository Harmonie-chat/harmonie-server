using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Conversations.DeleteMessageAttachment;
using Harmonie.Application.Features.Conversations.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
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
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IConversationParticipantRepository> _participantRepositoryMock;
    private readonly MessageEditDeleteOrchestrator _orchestrator;
    private readonly DeleteMessageAttachmentHandler _handler;

    public DeleteConversationMessageAttachmentHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _conversationMessageRepositoryMock = new Mock<IMessageRepository>();
        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _participantRepositoryMock = new Mock<IConversationParticipantRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        var uploadedFileCleanupService = new UploadedFileCleanupService(
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            NullLogger<UploadedFileCleanupService>.Instance);

        _orchestrator = new MessageEditDeleteOrchestrator(
            _conversationMessageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            uploadedFileCleanupService);

        _handler = new DeleteMessageAttachmentHandler(
            _conversationRepositoryMock.Object,
            _participantRepositoryMock.Object,
            new Mock<IConversationMessageNotifier>().Object,
            NullLogger<ConversationMessageEditDeleteScope>.Instance,
            _orchestrator);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();
        var attachmentId = UploadedFileId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(new DeleteConversationMessageAttachmentInput(conversationId, messageId, attachmentId), callerId, TestContext.Current.CancellationToken);

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
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(
            new DeleteConversationMessageAttachmentInput(conversation.Id, MessageId.New(), UploadedFileId.New()),
            outsider,
            TestContext.Current.CancellationToken);

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
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantTwo, content: "hello");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _conversationMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(new DeleteConversationMessageAttachmentInput(conversation.Id, message.Id, attachmentId), participantOne, TestContext.Current.CancellationToken);

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
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne, content: "hello");
        var missingAttachmentId = UploadedFileId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _conversationMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _messageAttachmentRepositoryMock
            .Setup(x => x.DeleteAsync(message.Id, missingAttachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(
            new DeleteConversationMessageAttachmentInput(conversation.Id, message.Id, missingAttachmentId),
            participantOne,
            TestContext.Current.CancellationToken);

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
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne, content: "hello");
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(id: attachmentId, uploaderUserId: participantOne, fileName: "notes.txt", contentType: "text/plain", sizeBytes: 12, storageKey: "attachments/file.txt");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _conversationMessageRepositoryMock
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

        var response = await _handler.HandleAsync(new DeleteConversationMessageAttachmentInput(conversation.Id, message.Id, attachmentId), participantOne, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _messageAttachmentRepositoryMock.Verify(
            x => x.DeleteAsync(message.Id, attachmentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
