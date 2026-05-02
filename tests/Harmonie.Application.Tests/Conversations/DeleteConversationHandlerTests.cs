using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.DeleteConversation;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class DeleteConversationHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IConversationParticipantRepository> _participantRepositoryMock;
    private readonly Mock<IRealtimeGroupManager> _realtimeGroupManagerMock;
    private readonly Mock<IConversationNotifier> _conversationNotifierMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly DeleteConversationHandler _handler;

    public DeleteConversationHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _participantRepositoryMock = new Mock<IConversationParticipantRepository>();
        _realtimeGroupManagerMock = new Mock<IRealtimeGroupManager>();
        _conversationNotifierMock = new Mock<IConversationNotifier>();
        _userRepositoryMock = new Mock<IUserRepository>();

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
            _participantRepositoryMock.Object,
            _realtimeGroupManagerMock.Object,
            _conversationNotifierMock.Object,
            _userRepositoryMock.Object,
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

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversationId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _participantRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _participantRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()),
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
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), outsider, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _participantRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _participantRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Direct conversation tests ──

    [Fact]
    public async Task HandleAsync_WhenDirectConversation_ShouldHideAndReturnSuccess()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);
        var participant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participant));

        var response = await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenDirectConversation_ShouldCallUpdateWithHiddenAtUtc()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);
        var participant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participant));

        await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId, TestContext.Current.CancellationToken);

        _participantRepositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<ConversationParticipant>(p =>
                    p.ConversationId == conversation.Id &&
                    p.UserId == callerId &&
                    p.HiddenAtUtc.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _participantRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDirectConversation_ShouldNotNotifyParticipantLeft()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);
        var participant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participant));

        await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId, TestContext.Current.CancellationToken);

        _conversationNotifierMock.Verify(
            x => x.NotifyParticipantLeftAsync(
                It.IsAny<ConversationParticipantLeftNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDirectConversation_ShouldNotRemoveFromSignalRGroup()
    {
        var callerId = UserId.New();
        var otherId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(callerId, otherId);
        var participant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participant));

        await _handler.HandleAsync(new DeleteConversationInput(conversation.Id), callerId, TestContext.Current.CancellationToken);

        _realtimeGroupManagerMock.Verify(
            x => x.RemoveUserFromConversationGroupAsync(It.IsAny<UserId>(), It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Group conversation tests ──

    [Fact]
    public async Task HandleAsync_WhenGroupConversation_ShouldRemoveParticipantAndReturnSuccess()
    {
        var callerId = UserId.New();
        var groupConversation = ApplicationTestBuilders.CreateGroupConversation("Test Group");
        var participant = ApplicationTestBuilders.CreateConversationParticipant(groupConversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(groupConversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(groupConversation, participant));

        _participantRepositoryMock
            .Setup(x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var response = await _handler.HandleAsync(new DeleteConversationInput(groupConversation.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenGroupConversation_ShouldNotifyParticipantLeft()
    {
        var callerId = UserId.New();
        var groupConversation = ApplicationTestBuilders.CreateGroupConversation("Test Group");
        var participant = ApplicationTestBuilders.CreateConversationParticipant(groupConversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(groupConversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(groupConversation, participant));

        _participantRepositoryMock
            .Setup(x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await _handler.HandleAsync(new DeleteConversationInput(groupConversation.Id), callerId, TestContext.Current.CancellationToken);

        _conversationNotifierMock.Verify(
            x => x.NotifyParticipantLeftAsync(
                It.Is<ConversationParticipantLeftNotification>(n =>
                    n.ConversationId == groupConversation.Id && n.UserId == callerId && n.Username == string.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGroupConversationLastParticipantLeaves_ShouldDeleteConversation()
    {
        var callerId = UserId.New();
        var groupConversation = ApplicationTestBuilders.CreateGroupConversation("Test Group");
        var participant = ApplicationTestBuilders.CreateConversationParticipant(groupConversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(groupConversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(groupConversation, participant));

        _participantRepositoryMock
            .Setup(x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _handler.HandleAsync(new DeleteConversationInput(groupConversation.Id), callerId, TestContext.Current.CancellationToken);

        _conversationRepositoryMock.Verify(
            x => x.DeleteAsync(groupConversation.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGroupConversationParticipantsRemain_ShouldNotDeleteConversation()
    {
        var callerId = UserId.New();
        var groupConversation = ApplicationTestBuilders.CreateGroupConversation("Test Group");
        var participant = ApplicationTestBuilders.CreateConversationParticipant(groupConversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(groupConversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(groupConversation, participant));

        _participantRepositoryMock
            .Setup(x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await _handler.HandleAsync(new DeleteConversationInput(groupConversation.Id), callerId, TestContext.Current.CancellationToken);

        _conversationRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGroupConversationNotifierThrows_ShouldStillRemoveParticipant()
    {
        var callerId = UserId.New();
        var groupConversation = ApplicationTestBuilders.CreateGroupConversation("Test Group");
        var participant = ApplicationTestBuilders.CreateConversationParticipant(groupConversation.Id, callerId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(groupConversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(groupConversation, participant));

        _participantRepositoryMock
            .Setup(x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _conversationNotifierMock
            .Setup(x => x.NotifyParticipantLeftAsync(
                It.IsAny<ConversationParticipantLeftNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(new DeleteConversationInput(groupConversation.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _participantRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<ConversationParticipant>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
