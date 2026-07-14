using FluentAssertions;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ConversationParticipantTests
{
    [Fact]
    public void Create_WithValidIds_ShouldSucceed()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        var result = ConversationParticipant.Create(conversationId, userId, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ConversationId.Should().Be(conversationId);
        result.Value.UserId.Should().Be(userId);
        result.Value.HiddenAtUtc.Should().BeNull();
        result.Value.JoinedAtUtc.Should().Be(TestTime.UtcNow);
    }

    [Fact]
    public void Create_WithNullConversationId_ShouldFail()
    {
        var result = ConversationParticipant.Create(null!, UserId.New(), TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Conversation ID is required");
    }

    [Fact]
    public void Create_WithNullUserId_ShouldFail()
    {
        var result = ConversationParticipant.Create(ConversationId.New(), null!, TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User ID is required");
    }

    [Fact]
    public void Hide_ShouldSetHiddenAtUtc()
    {
        var participant = ConversationParticipant.Rehydrate(
            ConversationId.New(), UserId.New(), TestTime.UtcNow, hiddenAtUtc: null);

        participant.Hide(TestTime.UtcNow.AddMinutes(1));

        participant.HiddenAtUtc.Should().NotBeNull();
        participant.HiddenAtUtc.Should().Be(TestTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Unhide_ShouldClearHiddenAtUtc()
    {
        var participant = ConversationParticipant.Rehydrate(
            ConversationId.New(), UserId.New(), TestTime.UtcNow, hiddenAtUtc: TestTime.UtcNow);

        participant.Unhide();

        participant.HiddenAtUtc.Should().BeNull();
    }

    [Fact]
    public void Rehydrate_WithNullConversationId_ShouldThrow()
    {
        var act = () => ConversationParticipant.Rehydrate(
            null!, UserId.New(), TestTime.UtcNow, hiddenAtUtc: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rehydrate_WithNullUserId_ShouldThrow()
    {
        var act = () => ConversationParticipant.Rehydrate(
            ConversationId.New(), null!, TestTime.UtcNow, hiddenAtUtc: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rehydrate_ShouldPreserveHiddenAtUtc()
    {
        var hiddenAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        var participant = ConversationParticipant.Rehydrate(
            ConversationId.New(), UserId.New(), TestTime.UtcNow, hiddenAtUtc: hiddenAt);

        participant.HiddenAtUtc.Should().Be(hiddenAt);
    }
}
