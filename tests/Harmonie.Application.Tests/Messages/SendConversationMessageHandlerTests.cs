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
            conversationId,
            new SendMessageRequest("hello"),
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

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendMessageRequest("hello"),
            outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnContentEmpty()
    {
        var conversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendMessageRequest("   "),
            conversation.User1Id);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldPersistCommitAndNotify()
    {
        var conversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());
        Message? persistedMessage = null;

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendMessageRequest("  hello dm  "),
            conversation.User1Id);

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
                    && n.AuthorUserId == conversation.User1Id
                    && n.Content == "hello dm"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithOwnedAttachmentFiles_ShouldPersistAttachmentsAndReturnThem()
    {
        var conversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());
        var attachment = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: conversation.User1Id, fileName: "report.pdf", contentType: "application/pdf");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<UploadedFileId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([attachment]);

        Message? persistedMessage = null;
        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendMessageRequest("hello dm", [attachment.Id.ToString()]),
            conversation.User1Id);

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
        var conversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<ConversationMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendMessageRequest("hello"),
            conversation.User1Id);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

}
