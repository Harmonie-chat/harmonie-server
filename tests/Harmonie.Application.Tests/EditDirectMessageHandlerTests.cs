using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.EditDirectMessage;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class EditDirectMessageHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IDirectMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IDirectMessageNotifier> _directMessageNotifierMock;
    private readonly EditDirectMessageHandler _handler;

    public EditDirectMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IDirectMessageRepository>();
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
            .Setup(x => x.NotifyMessageUpdatedAsync(It.IsAny<DirectMessageUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new EditDirectMessageHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _directMessageNotifierMock.Object,
            NullLogger<EditDirectMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnMessageContentEmpty()
    {
        var response = await _handler.HandleAsync(
            ConversationId.New(),
            DirectMessageId.New(),
            new EditDirectMessageRequest("   "),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(
            conversationId,
            DirectMessageId.New(),
            new EditDirectMessageRequest("updated content"),
            callerId);

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

        var response = await _handler.HandleAsync(
            conversation.Id,
            DirectMessageId.New(),
            new EditDirectMessageRequest("updated content"),
            outsider);

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
        var messageId = DirectMessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DirectMessage?)null);

        var response = await _handler.HandleAsync(
            conversation.Id,
            messageId,
            new EditDirectMessageRequest("updated content"),
            participantOne);

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
        var messageId = DirectMessageId.New();
        var messageFromOtherConversation = CreateDirectMessage(ConversationId.New(), participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOtherConversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            messageId,
            new EditDirectMessageRequest("updated content"),
            participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnEditForbidden()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = DirectMessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            conversation.Id,
            messageId,
            new EditDirectMessageRequest("updated content"),
            participantOne);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldReturnUpdatedMessage()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = DirectMessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            conversation.Id,
            messageId,
            new EditDirectMessageRequest("  updated content  "),
            participantOne);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("updated content");
        response.Data.ConversationId.Should().Be(conversation.Id.ToString());
        response.Data.AuthorUserId.Should().Be(participantOne.ToString());
        response.Data.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldPersistCommitAndNotify()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = DirectMessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            conversation.Id,
            messageId,
            new EditDirectMessageRequest("updated content"),
            participantOne);

        response.Success.Should().BeTrue();
        _directMessageRepositoryMock.Verify(
            x => x.UpdateContentAsync(
                It.Is<DirectMessage>(m =>
                    m.Id == message.Id
                    && m.Content.Value == "updated content"
                    && m.UpdatedAtUtc != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _directMessageNotifierMock.Verify(
            x => x.NotifyMessageUpdatedAsync(
                It.Is<DirectMessageUpdatedNotification>(n =>
                    n.MessageId == message.Id
                    && n.ConversationId == conversation.Id
                    && n.Content == "updated content"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = CreateConversation(participantOne, participantTwo);
        var messageId = DirectMessageId.New();
        var message = CreateDirectMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageUpdatedAsync(
                It.IsAny<DirectMessageUpdatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            conversation.Id,
            messageId,
            new EditDirectMessageRequest("updated content"),
            participantOne);

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

    private static DirectMessage CreateDirectMessage(ConversationId conversationId, UserId authorUserId)
    {
        var contentResult = ChannelMessageContent.Create("original content");
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create test direct message.");

        return DirectMessage.Rehydrate(
            DirectMessageId.New(),
            conversationId,
            authorUserId,
            contentResult.Value,
            DateTime.UtcNow.AddMinutes(-1),
            updatedAtUtc: null,
            deletedAtUtc: null);
    }
}
