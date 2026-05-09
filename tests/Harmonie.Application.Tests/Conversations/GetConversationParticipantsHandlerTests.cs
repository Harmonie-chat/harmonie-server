using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetConversationParticipants;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class GetConversationParticipantsHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetConversationParticipantsHandler _handler;

    public GetConversationParticipantsHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _handler = new GetConversationParticipantsHandler(
            _conversationRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccessWithAllParticipants?)null);

        var response = await _handler.HandleAsync(conversationId, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotParticipant_ShouldReturnForbidden()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(userId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(
                conversation,
                CallerParticipant: null,
                AllParticipants: [],
                CallerUsername: null,
                CallerDisplayName: null));

        var response = await _handler.HandleAsync(conversationId, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNoOtherParticipants_ShouldReturnCallerOnly()
    {
        var userId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(userId, UserId.New());
        var participant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, userId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(
                conversation,
                CallerParticipant: participant,
                AllParticipants: [participant],
                CallerUsername: null,
                CallerDisplayName: null));

        var response = await _handler.HandleAsync(conversation.Id, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Participants.Should().HaveCount(1);
        response.Data.Participants[0].UserId.Should().Be(userId.Value);
        response.Data.Participants[0].Username.Should().Be("Unknown");
        response.Data.Participants[0].IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantsHaveUserProfiles_ShouldReturnEnrichedData()
    {
        var userId = UserId.New();
        var otherUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(userId, otherUserId);

        var callerParticipant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, userId);
        var otherParticipant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, otherUserId);
        var otherUser = ApplicationTestBuilders.CreateUser(otherUserId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(
                conversation,
                CallerParticipant: callerParticipant,
                AllParticipants: [callerParticipant, otherParticipant],
                CallerUsername: null,
                CallerDisplayName: null));

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(
                It.Is<IReadOnlyList<UserId>>(ids => ids.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherUser]);

        var response = await _handler.HandleAsync(conversation.Id, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Participants.Should().HaveCount(2);

        response.Data.Participants[0].UserId.Should().Be(userId.Value);
        response.Data.Participants[0].Username.Should().Be("Unknown");

        response.Data.Participants[1].UserId.Should().Be(otherUserId.Value);
        response.Data.Participants[1].Username.Should().Be(otherUser.Username.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenParticipantIsHidden_ShouldReflectIsHidden()
    {
        var userId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(userId, UserId.New());
        var hiddenParticipant = ConversationParticipant.Rehydrate(
            conversation.Id, userId, DateTime.UtcNow.AddDays(-1), hiddenAtUtc: DateTime.UtcNow);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(
                conversation,
                CallerParticipant: hiddenParticipant,
                AllParticipants: [hiddenParticipant],
                CallerUsername: null,
                CallerDisplayName: null));

        var response = await _handler.HandleAsync(conversation.Id, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Participants.Should().HaveCount(1);
        response.Data.Participants[0].IsHidden.Should().BeTrue();
    }
}
