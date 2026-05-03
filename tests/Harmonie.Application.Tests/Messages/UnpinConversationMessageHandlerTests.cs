using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.UnpinMessage;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class UnpinConversationMessageHandlerTests
{
    private const string TestUsername = "testuser";
    private const string TestDisplayName = "Test User";

    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IPinnedMessageRepository> _pinnedMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IPinNotifier> _pinNotifierMock;
    private readonly UnpinMessageHandler _handler;

    public UnpinConversationMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _pinnedMessageRepositoryMock = new Mock<IPinnedMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _pinNotifierMock = new Mock<IPinNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _pinNotifierMock
            .Setup(x => x.NotifyMessageUnpinnedInConversationAsync(It.IsAny<ConversationPinRemovedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new UnpinMessageHandler(
            _conversationRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _pinnedMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _pinNotifierMock.Object,
            NullLogger<UnpinMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(new ConversationUnpinMessageInput(conversationId, MessageId.New()), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnConversationAccessDenied()
    {
        var conversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(new ConversationUnpinMessageInput(conversation.Id, MessageId.New()), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageDoesNotExist_ShouldReturnPinMessageNotFound()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);
        var messageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj, TestUsername, TestDisplayName));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var response = await _handler.HandleAsync(new ConversationUnpinMessageInput(conversation.Id, messageId), participant, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Pin.MessageNotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageBelongsToAnotherConversation_ShouldReturnPinMessageNotFound()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);
        var messageId = MessageId.New();
        var messageFromOtherConversation = ApplicationTestBuilders.CreateConversationMessage(ConversationId.New(), participant, content: "original content");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj, TestUsername, TestDisplayName));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOtherConversation);

        var response = await _handler.HandleAsync(new ConversationUnpinMessageInput(conversation.Id, messageId), participant, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Pin.MessageNotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantUnpins_ShouldReturnSuccess()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participant, content: "original content");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj, TestUsername, TestDisplayName));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(new ConversationUnpinMessageInput(conversation.Id, messageId), participant, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantUnpins_ShouldDeleteCommitAndNotify()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participant, content: "original content");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj, TestUsername, TestDisplayName));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(new ConversationUnpinMessageInput(conversation.Id, messageId), participant, TestContext.Current.CancellationToken);

        _pinnedMessageRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<MessageId>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _pinNotifierMock.Verify(
            x => x.NotifyMessageUnpinnedInConversationAsync(
                It.Is<ConversationPinRemovedNotification>(n =>
                    n.ConversationId == conversation.Id &&
                    n.ConversationName == null &&
                    n.ConversationType == "Direct" &&
                    n.MessageId == messageId &&
                    n.UnpinnedByUserId == participant &&
                    n.UnpinnedByUsername == TestUsername &&
                    n.UnpinnedByDisplayName == TestDisplayName),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participant, content: "original content");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj, TestUsername, TestDisplayName));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _pinNotifierMock
            .Setup(x => x.NotifyMessageUnpinnedInConversationAsync(It.IsAny<ConversationPinRemovedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(new ConversationUnpinMessageInput(conversation.Id, messageId), participant, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
