using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.SendDirectMessage;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class SendDirectMessageHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IDirectMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IDirectMessageNotifier> _directMessageNotifierMock;
    private readonly SendDirectMessageHandler _handler;

    public SendDirectMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IDirectMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _directMessageNotifierMock = new Mock<IDirectMessageNotifier>();

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<DirectMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new SendDirectMessageHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _directMessageNotifierMock.Object,
            NullLogger<SendDirectMessageHandler>.Instance);
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
            new SendDirectMessageRequest("hello"),
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
        var conversation = CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendDirectMessageRequest("hello"),
            outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnContentEmpty()
    {
        var conversation = CreateConversation(UserId.New(), UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendDirectMessageRequest("   "),
            conversation.User1Id);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldPersistCommitAndNotify()
    {
        var conversation = CreateConversation(UserId.New(), UserId.New());
        DirectMessage? persistedMessage = null;

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DirectMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DirectMessage, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendDirectMessageRequest("  hello dm  "),
            conversation.User1Id);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("hello dm");
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Content.Value.Should().Be("hello dm");
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _directMessageNotifierMock.Verify(
            x => x.NotifyMessageCreatedAsync(
                It.Is<DirectMessageCreatedNotification>(n =>
                    n.ConversationId == conversation.Id
                    && n.AuthorUserId == conversation.User1Id
                    && n.Content == "hello dm"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var conversation = CreateConversation(UserId.New(), UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DirectMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<DirectMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SendDirectMessageRequest("hello"),
            conversation.User1Id);

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
}
