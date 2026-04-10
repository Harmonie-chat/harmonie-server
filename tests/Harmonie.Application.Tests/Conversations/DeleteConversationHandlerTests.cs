using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.DeleteConversation;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class DeleteConversationHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IRealtimeGroupManager> _realtimeGroupManagerMock;
    private readonly Mock<IConversationNotifier> _conversationNotifierMock;
    private readonly DeleteConversationHandler _handler;

    public DeleteConversationHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _realtimeGroupManagerMock = new Mock<IRealtimeGroupManager>();
        _conversationNotifierMock = new Mock<IConversationNotifier>();

        _conversationNotifierMock
            .Setup(x => x.NotifyParticipantLeftAsync(
                It.IsAny<ConversationParticipantLeftNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _realtimeGroupManagerMock
            .Setup(x => x.RemoveUserFromConversationGroupAsync(
                It.IsAny<UserId>(),
                It.IsAny<ConversationId>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new DeleteConversationHandler(
            _conversationRepositoryMock.Object,
            _realtimeGroupManagerMock.Object,
            _conversationNotifierMock.Object,
            NullLogger<DeleteConversationHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversationId), callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _conversationRepositoryMock.Verify(
            x => x.RemoveParticipantAsync(It.IsAny<ConversationId>(), It.IsAny<UserId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnAccessDenied()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: false));

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), outsider);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _conversationRepositoryMock.Verify(
            x => x.RemoveParticipantAsync(It.IsAny<ConversationId>(), It.IsAny<UserId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantLeaves_ShouldReturnSuccess()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: true));

        _conversationRepositoryMock
            .Setup(x => x.RemoveParticipantAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantLeaves_ShouldNotifyAndRemoveFromSignalRGroup()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: true));

        _conversationRepositoryMock
            .Setup(x => x.RemoveParticipantAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId);

        _conversationNotifierMock.Verify(
            x => x.NotifyParticipantLeftAsync(
                It.Is<ConversationParticipantLeftNotification>(n =>
                    n.ConversationId == conversation.Id && n.UserId == callerId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _realtimeGroupManagerMock.Verify(
            x => x.RemoveUserFromConversationGroupAsync(callerId, conversation.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenLastParticipantLeaves_ShouldDeleteConversation()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: true));

        _conversationRepositoryMock
            .Setup(x => x.RemoveParticipantAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId);

        _conversationRepositoryMock.Verify(
            x => x.DeleteAsync(conversation.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantsRemain_ShouldNotDeleteConversation()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: true));

        _conversationRepositoryMock
            .Setup(x => x.RemoveParticipantAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId);

        _conversationRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceedAndRemoveParticipant()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: true));

        _conversationRepositoryMock
            .Setup(x => x.RemoveParticipantAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _conversationNotifierMock
            .Setup(x => x.NotifyParticipantLeftAsync(
                It.IsAny<ConversationParticipantLeftNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId);

        response.Success.Should().BeTrue();
        _conversationRepositoryMock.Verify(
            x => x.RemoveParticipantAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
