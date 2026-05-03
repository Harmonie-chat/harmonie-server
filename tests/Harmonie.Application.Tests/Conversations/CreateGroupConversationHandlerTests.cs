using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.CreateGroupConversation;
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

public sealed class CreateGroupConversationHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRealtimeGroupManager> _realtimeGroupManagerMock;
    private readonly Mock<IConversationNotifier> _conversationNotifierMock;
    private readonly CreateGroupConversationHandler _handler;

    public CreateGroupConversationHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _realtimeGroupManagerMock = new Mock<IRealtimeGroupManager>();
        _conversationNotifierMock = new Mock<IConversationNotifier>();

        _realtimeGroupManagerMock
            .Setup(x => x.AddUserToConversationGroupAsync(It.IsAny<UserId>(), It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _conversationNotifierMock
            .Setup(x => x.NotifyConversationCreatedAsync(It.IsAny<ConversationCreatedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new CreateGroupConversationHandler(
            _conversationRepositoryMock.Object,
            _userRepositoryMock.Object,
            _realtimeGroupManagerMock.Object,
            _conversationNotifierMock.Object,
            NullLogger<CreateGroupConversationHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotInParticipantList_ShouldReturnAccessDenied()
    {
        var caller = UserId.New();
        var participantA = UserId.New();
        var participantB = UserId.New();

        var response = await _handler.HandleAsync(
            new CreateGroupConversationRequest("Team Chat", [participantA.Value, participantB.Value]),
            caller,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _conversationRepositoryMock.Verify(x => x.CreateGroupAsync(
            It.IsAny<string?>(), It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAParticipantDoesNotExist_ShouldReturnUserNotFound()
    {
        var caller = UserId.New();
        var participantB = UserId.New();
        var callerUser = ApplicationTestBuilders.CreateUser(caller);

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([callerUser]); // participantB is missing

        var response = await _handler.HandleAsync(
            new CreateGroupConversationRequest("Team Chat", [caller.Value, participantB.Value]),
            caller,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
        _conversationRepositoryMock.Verify(x => x.CreateGroupAsync(
            It.IsAny<string?>(), It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnGroupConversationResponse()
    {
        var caller = UserId.New();
        var participantB = UserId.New();
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Team Chat", DateTime.UtcNow);

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ApplicationTestBuilders.CreateUser(caller), ApplicationTestBuilders.CreateUser(participantB)]);

        _conversationRepositoryMock
            .Setup(x => x.CreateGroupAsync(
                "Team Chat",
                It.Is<IReadOnlyList<UserId>>(ids => ids.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            new CreateGroupConversationRequest("Team Chat", [caller.Value, participantB.Value]),
            caller,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Type.Should().Be("group");
        response.Data.Name.Should().Be("Team Chat");
        response.Data.ConversationId.Should().Be(conversation.Id.Value);
        response.Data.Participants.Should().HaveCount(2);

        _conversationNotifierMock.Verify(
            x => x.NotifyConversationCreatedAsync(
                It.Is<ConversationCreatedNotification>(n =>
                    n.ConversationId == conversation.Id
                    && n.Name == "Team Chat"
                    && n.ConversationType == "Group"
                    && n.Participants.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNullName_ShouldSucceed()
    {
        var caller = UserId.New();
        var participantB = UserId.New();
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, null, DateTime.UtcNow);

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ApplicationTestBuilders.CreateUser(caller), ApplicationTestBuilders.CreateUser(participantB)]);

        _conversationRepositoryMock
            .Setup(x => x.CreateGroupAsync(
                null,
                It.IsAny<IReadOnlyList<UserId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            new CreateGroupConversationRequest(null, [caller.Value, participantB.Value]),
            caller,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().BeNull();
        response.Data.Type.Should().Be("group");
    }
}
