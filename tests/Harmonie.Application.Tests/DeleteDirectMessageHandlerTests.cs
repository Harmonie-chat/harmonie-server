using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.DeleteDirectMessage;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class DeleteDirectMessageHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IDirectMessageNotifier> _directMessageNotifierMock;
    private readonly DeleteDirectMessageHandler _handler;

    public DeleteDirectMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _directMessageNotifierMock = new Mock<IDirectMessageNotifier>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageDeletedAsync(It.IsAny<DirectMessageDeletedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new DeleteDirectMessageHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _directMessageNotifierMock.Object,
            NullLogger<DeleteDirectMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(conversationId, MessageId.New(), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnConversationAccessDenied()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var outsider = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(conversation.Id, MessageId.New(), outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageDoesNotExist_ShouldReturnMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageBelongsToAnotherConversation_ShouldReturnMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = CreateDirectMessage(ConversationId.New(), participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnDeleteForbidden()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesOwnMessage_ShouldReturnSuccess()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDeletesOwnMessage_ShouldPersistCommitAndNotify()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        _directMessageRepositoryMock.Verify(
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

        _directMessageNotifierMock.Verify(
            x => x.NotifyMessageDeletedAsync(
                It.Is<DirectMessageDeletedNotification>(n =>
                    n.MessageId == messageId
                    && n.ConversationId == conversation.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageDeletedAsync(
                It.IsAny<DirectMessageDeletedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Conversation CreateConversation(UserId user1Id, UserId user2Id)
    {
        var result = Conversation.Create(user1Id, user2Id);
        if (result.IsFailure || result.Value is null)
            throw new InvalidOperationException("Failed to create test conversation.");

        return result.Value;
    }

    private static Message CreateDirectMessage(ConversationId conversationId, UserId authorUserId)
    {
        var contentResult = MessageContent.Create("original content");
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create test direct message.");

        return Message.Rehydrate(
            id: MessageId.New(),
            channelId: null,
            conversationId: conversationId,
            authorUserId: authorUserId,
            content: contentResult.Value,
            createdAtUtc: DateTime.UtcNow.AddMinutes(-1),
            updatedAtUtc: null,
            deletedAtUtc: null);
    }
}
