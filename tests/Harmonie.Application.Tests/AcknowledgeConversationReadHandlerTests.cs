using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.AcknowledgeRead;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class AcknowledgeConversationReadHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IConversationReadStateRepository> _conversationReadStateRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly AcknowledgeReadHandler _handler;

    public AcknowledgeConversationReadHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _conversationReadStateRepositoryMock = new Mock<IConversationReadStateRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _handler = new AcknowledgeReadHandler(
            _conversationRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _conversationReadStateRepositoryMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AcknowledgeReadHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(conversationId, null, callerId);

        response.Success.Should().BeFalse();
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

        var response = await _handler.HandleAsync(conversation.Id, null, outsider);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageIdProvidedAndNotFound_ShouldReturnMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeFalse();
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
        var messageFromOther = CreateConversationMessage(ConversationId.New(), participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOther);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageIdProvided_ShouldUpsertAndCommit()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(conversation.Id, messageId, participantOne);

        response.Success.Should().BeTrue();

        _conversationReadStateRepositoryMock.Verify(
            x => x.UpsertAsync(participantOne, conversation.Id, messageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNullMessageIdAndConversationHasMessages_ShouldUpsertWithLatest()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var latestMessageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _messageRepositoryMock
            .Setup(x => x.GetLatestConversationMessageIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestMessageId);

        var response = await _handler.HandleAsync(conversation.Id, null, participantOne);

        response.Success.Should().BeTrue();

        _conversationReadStateRepositoryMock.Verify(
            x => x.UpsertAsync(participantOne, conversation.Id, latestMessageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNullMessageIdAndConversationIsEmpty_ShouldReturnSuccessWithoutUpsert()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _messageRepositoryMock
            .Setup(x => x.GetLatestConversationMessageIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageId?)null);

        var response = await _handler.HandleAsync(conversation.Id, null, participantOne);

        response.Success.Should().BeTrue();

        _conversationReadStateRepositoryMock.Verify(
            x => x.UpsertAsync(It.IsAny<UserId>(), It.IsAny<ConversationId>(), It.IsAny<MessageId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Conversation CreateConversation(UserId user1Id, UserId user2Id)
    {
        var result = Conversation.Create(user1Id, user2Id);
        if (result.IsFailure || result.Value is null)
            throw new InvalidOperationException("Failed to create test conversation.");

        return result.Value;
    }

    private static Message CreateConversationMessage(ConversationId conversationId, UserId authorUserId)
    {
        var contentResult = MessageContent.Create("test content");
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create test conversation message.");

        return Message.Rehydrate(
            id: MessageId.New(),
            channelId: null,
            conversationId: conversationId,
            authorUserId: authorUserId,
            content: contentResult.Value,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);
    }
}
