using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.RemoveReaction;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class RemoveConversationReactionHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageReactionRepository> _reactionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IReactionNotifier> _reactionNotifierMock;
    private readonly RemoveReactionHandler _handler;

    public RemoveConversationReactionHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _reactionRepositoryMock = new Mock<IMessageReactionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _reactionNotifierMock = new Mock<IReactionNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _reactionNotifierMock
            .Setup(x => x.NotifyReactionRemovedFromConversationAsync(It.IsAny<ConversationReactionRemovedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new RemoveReactionHandler(
            _conversationRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _reactionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _reactionNotifierMock.Object,
            NullLogger<RemoveReactionHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(new ConversationRemoveReactionInput(conversationId, MessageId.New(), "👍"), callerId, TestContext.Current.CancellationToken);

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
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(new ConversationRemoveReactionInput(conversation.Id, MessageId.New(), "👍"), outsider, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageDoesNotExist_ShouldReturnReactionMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var response = await _handler.HandleAsync(new ConversationRemoveReactionInput(conversation.Id, messageId, "👍"), participantOne, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageBelongsToAnotherConversation_ShouldReturnReactionMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(ConversationId.New(), participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(new ConversationRemoveReactionInput(conversation.Id, messageId, "👍"), participantOne, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantRemovesReaction_ShouldReturnSuccess()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(new ConversationRemoveReactionInput(conversation.Id, messageId, "❤"), participantOne, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantRemovesReaction_ShouldDeleteCommitAndNotify()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        await _handler.HandleAsync(new ConversationRemoveReactionInput(conversation.Id, messageId, "❤"), participantOne, TestContext.Current.CancellationToken);

        _reactionRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<MessageId>(), It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _reactionNotifierMock.Verify(
            x => x.NotifyReactionRemovedFromConversationAsync(
                It.Is<ConversationReactionRemovedNotification>(n =>
                    n.ConversationId == conversation.Id &&
                    n.MessageId == messageId &&
                    n.UserId == participantOne &&
                    n.Emoji == "❤"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _reactionNotifierMock
            .Setup(x => x.NotifyReactionRemovedFromConversationAsync(It.IsAny<ConversationReactionRemovedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(new ConversationRemoveReactionInput(conversation.Id, messageId, "👍"), participantOne, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

}
