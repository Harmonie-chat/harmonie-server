using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.SendMessage;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class SendConversationMessageHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IConversationMessageNotifier> _directMessageNotifierMock;
    private readonly SendMessageHandler _handler;

    public SendConversationMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IMessageRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _directMessageNotifierMock = new Mock<IConversationMessageNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<ConversationMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new SendMessageHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            new MessageAttachmentResolver(_uploadedFileRepositoryMock.Object),
            _unitOfWorkMock.Object,
            _directMessageNotifierMock.Object,
            NullLogger<SendMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversationId, "hello"),
            userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotParticipant_ShouldReturnAccessDenied()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationRepositoryMock
            .Setup(x => x.IsParticipantAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello"),
            outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnContentEmpty()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "   "),
            currentUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldPersistCommitAndNotify()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        Message? persistedMessage = null;

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationRepositoryMock
            .Setup(x => x.IsParticipantAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "  hello dm  "),
            currentUserId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("hello dm");
        response.Data.Attachments.Should().BeEmpty();
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Content.Value.Should().Be("hello dm");
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _directMessageNotifierMock.Verify(
            x => x.NotifyMessageCreatedAsync(
                It.Is<ConversationMessageCreatedNotification>(n =>
                    n.ConversationId == conversation.Id
                    && n.AuthorUserId == currentUserId
                    && n.Content == "hello dm"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithOwnedAttachmentFiles_ShouldPersistAttachmentsAndReturnThem()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        var attachment = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: currentUserId, fileName: "report.pdf", contentType: "application/pdf");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationRepositoryMock
            .Setup(x => x.IsParticipantAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<UploadedFileId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([attachment]);

        Message? persistedMessage = null;
        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello dm", [attachment.Id]),
            currentUserId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Attachments.Should().ContainSingle();
        response.Data.Attachments[0].FileId.Should().Be(attachment.Id.ToString());
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Attachments.Should().ContainSingle();
        persistedMessage.Attachments[0].FileId.Should().Be(attachment.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _conversationRepositoryMock
            .Setup(x => x.IsParticipantAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<ConversationMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello"),
            currentUserId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
