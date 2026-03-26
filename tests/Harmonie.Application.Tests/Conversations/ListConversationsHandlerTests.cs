using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class ListConversationsHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly ListConversationsHandler _handler;

    public ListConversationsHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _handler = new ListConversationsHandler(
            _conversationRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasNoConversations_ShouldReturnEmptyCollection()
    {
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetUserConversationsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _handler.HandleAsync(Unit.Value, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasConversations_ShouldReturnMappedSummaries()
    {
        var userId = UserId.New();
        var aliceId = UserId.New();
        var bobId = UserId.New();
        var usernameAlice = Username.Create("alice");
        var usernameBob = Username.Create("bob");
        usernameAlice.IsSuccess.Should().BeTrue();
        usernameBob.IsSuccess.Should().BeTrue();
        usernameAlice.Value.Should().NotBeNull();
        usernameBob.Value.Should().NotBeNull();

        var firstCreatedAt = DateTime.UtcNow.AddMinutes(-10);
        var secondCreatedAt = DateTime.UtcNow.AddMinutes(-5);

        _conversationRepositoryMock
            .Setup(x => x.GetUserConversationsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UserConversationSummary(
                    ConversationId.New(),
                    ConversationType.Direct,
                    null,
                    [new ConversationParticipantSummary(bobId, usernameBob.Value!)],
                    secondCreatedAt),
                new UserConversationSummary(
                    ConversationId.New(),
                    ConversationType.Direct,
                    null,
                    [new ConversationParticipantSummary(aliceId, usernameAlice.Value!)],
                    firstCreatedAt)
            ]);

        var response = await _handler.HandleAsync(Unit.Value, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Conversations.Should().HaveCount(2);
        response.Data.Conversations[0].Participants.Should().ContainSingle(p => p.Username == "bob");
        response.Data.Conversations[0].CreatedAtUtc.Should().Be(secondCreatedAt);
        response.Data.Conversations[1].Participants.Should().ContainSingle(p => p.Username == "alice");
        response.Data.Conversations[1].CreatedAtUtc.Should().Be(firstCreatedAt);
    }
}
