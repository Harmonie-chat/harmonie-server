using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetReactionUsers;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class GetConversationReactionUsersHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageReactionRepository> _reactionRepositoryMock;
    private readonly GetReactionUsersHandler _handler;

    public GetConversationReactionUsersHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _reactionRepositoryMock = new Mock<IMessageReactionRepository>();

        _handler = new GetReactionUsersHandler(
            _conversationRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _reactionRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenNoReactions_ShouldReturnEmptyUsers()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation,
                Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _reactionRepositoryMock
            .Setup(x => x.GetReactionUsersAsync(messageId, "❤️", It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReactionUsersPage(Array.Empty<ReactionUser>(), 0, null));

        var response = await _handler.HandleAsync(
            new GetConversationReactionUsersInput(conversation.Id, messageId, "❤️"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Users.Should().BeEmpty();
        response.Data.TotalCount.Should().Be(0);
        response.Data.Emoji.Should().Be("❤️");
    }

    [Fact]
    public async Task HandleAsync_WithReactions_ShouldReturnUsers()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation,
                Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _reactionRepositoryMock
            .Setup(x => x.GetReactionUsersAsync(messageId, "❤️", It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReactionUsersPage(
                new[] { new ReactionUser(participantOne.Value, "user1", "User One") },
                1,
                null));

        var response = await _handler.HandleAsync(
            new GetConversationReactionUsersInput(conversation.Id, messageId, "❤️"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Users.Should().HaveCount(1);
        response.Data.TotalCount.Should().Be(1);
        response.Data.Users[0].UserId.Should().Be(participantOne.Value);
        response.Data.Users[0].Username.Should().Be("user1");
        response.Data.Users[0].DisplayName.Should().Be("User One");
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
            new GetConversationReactionUsersInput(conversationId, MessageId.New(), "👍"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
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

        var response = await _handler.HandleAsync(
            new GetConversationReactionUsersInput(conversation.Id, MessageId.New(), "👍"),
            outsider,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }
}
