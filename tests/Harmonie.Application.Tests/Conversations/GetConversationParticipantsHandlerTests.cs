using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetConversationParticipants;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class GetConversationParticipantsHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly GetConversationParticipantsHandler _handler;

    public GetConversationParticipantsHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _handler = new GetConversationParticipantsHandler(_conversationRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetParticipantsWithProfilesAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccessWithParticipantProfiles?)null);

        var response = await _handler.HandleAsync(conversationId, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotParticipant_ShouldReturnForbidden()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetParticipantsWithProfilesAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithParticipantProfiles(
                CallerParticipant: null,
                Participants: []));

        var response = await _handler.HandleAsync(conversationId, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsParticipant_ShouldReturnParticipantProfiles()
    {
        var userId = UserId.New();
        var otherUserId = UserId.New();
        var conversationId = ConversationId.New();
        var now = DateTime.UtcNow;

        var callerParticipant = ConversationParticipant.Rehydrate(
            conversationId, userId, DateTime.UtcNow, hiddenAtUtc: null);

        var profiles = new[]
        {
            new ParticipantProfile(
                UserId: userId.Value,
                Username: "caller",
                DisplayName: "Caller",
                AvatarFileId: null,
                AvatarColor: "#111",
                AvatarIcon: null,
                AvatarBg: null,
                JoinedAtUtc: now),
            new ParticipantProfile(
                UserId: otherUserId.Value,
                Username: "other",
                DisplayName: "Other User",
                AvatarFileId: Guid.NewGuid(),
                AvatarColor: null,
                AvatarIcon: "star",
                AvatarBg: "#222",
                JoinedAtUtc: now.AddDays(-1))
        };

        _conversationRepositoryMock
            .Setup(x => x.GetParticipantsWithProfilesAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithParticipantProfiles(callerParticipant, profiles));

        var response = await _handler.HandleAsync(conversationId, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.Participants.Should().HaveCount(2);

        var dto0 = response.Data.Participants[0];
        dto0.UserId.Should().Be(userId.Value);
        dto0.Username.Should().Be("caller");
        dto0.DisplayName.Should().Be("Caller");
        dto0.Avatar.Should().NotBeNull();
        dto0.Avatar!.Color.Should().Be("#111");
        dto0.Avatar.Icon.Should().BeNull();
        dto0.Avatar.Bg.Should().BeNull();
        dto0.JoinedAtUtc.Should().Be(now);

        var dto1 = response.Data.Participants[1];
        dto1.UserId.Should().Be(otherUserId.Value);
        dto1.Username.Should().Be("other");
        dto1.AvatarFileId.Should().NotBeNull();
        dto1.Avatar!.Icon.Should().Be("star");
        dto1.Avatar.Bg.Should().Be("#222");
        dto1.JoinedAtUtc.Should().Be(now.AddDays(-1));
    }
}
