using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.UpdateGroupConversation;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class UpdateGroupConversationHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IConversationNotifier> _conversationNotifierMock;
    private readonly UpdateGroupConversationHandler _handler;

    public UpdateGroupConversationHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _conversationNotifierMock = new Mock<IConversationNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _conversationNotifierMock
            .Setup(x => x.NotifyConversationUpdatedAsync(
                It.IsAny<ConversationUpdatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new UpdateGroupConversationHandler(
            _conversationRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _conversationNotifierMock.Object,
            NullLogger<UpdateGroupConversationHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversationId, "New Name"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnAccessDenied()
    {
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateGroupConversation("Original Name");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversation.Id, "New Name"),
            outsider,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationIsDirect_ShouldReturnInvalidType()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        var response = await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversation.Id, "New Name"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.InvalidConversationType);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGroupConversationUpdated_ShouldReturnSuccess()
    {
        var callerId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateGroupConversation("Original Name");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId)));

        var response = await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversation.Id, "New Name"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.ConversationId.Should().Be(conversation.Id.Value);
        response.Data.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task HandleAsync_WhenGroupConversationUpdated_ShouldPersistAndNotify()
    {
        var callerId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateGroupConversation("Original Name");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId)));

        await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversation.Id, "New Name"),
            callerId,
            TestContext.Current.CancellationToken);

        _conversationRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<Conversation>(c => c.Name == "New Name"), It.IsAny<CancellationToken>()),
            Times.Once);

        _conversationNotifierMock.Verify(
            x => x.NotifyConversationUpdatedAsync(
                It.Is<ConversationUpdatedNotification>(n =>
                    n.ConversationId == conversation.Id && n.Name == "New Name"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNameIsNull_ShouldResetToDefaultAndNotify()
    {
        var callerId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateGroupConversation("Original Name");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId)));

        var response = await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversation.Id, null),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().BeNull();

        _conversationRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<Conversation>(c => c.Name == null), It.IsAny<CancellationToken>()),
            Times.Once);

        _conversationNotifierMock.Verify(
            x => x.NotifyConversationUpdatedAsync(
                It.Is<ConversationUpdatedNotification>(n =>
                    n.ConversationId == conversation.Id && n.Name == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var callerId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateGroupConversation("Original Name");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId)));

        _conversationNotifierMock
            .Setup(x => x.NotifyConversationUpdatedAsync(
                It.IsAny<ConversationUpdatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            new UpdateGroupConversationInput(conversation.Id, "New Name"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _conversationRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
