using FluentAssertions;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ConversationTests
{
    [Fact]
    public void CreateDirect_WithDistinctUsers_ShouldSucceed()
    {
        var user1 = UserId.New();
        var user2 = UserId.New();

        var result = Conversation.CreateDirect(user1, user2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(ConversationType.Direct);
        result.Value.Name.Should().BeNull();
    }

    [Fact]
    public void CreateDirect_WithSameUserTwice_ShouldFail()
    {
        var userId = UserId.New();

        var result = Conversation.CreateDirect(userId, userId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateGroup_WithTwoOrMoreParticipants_ShouldSucceed()
    {
        var participants = new[] { UserId.New(), UserId.New(), UserId.New() };

        var result = Conversation.CreateGroup("My Group", participants);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(ConversationType.Group);
        result.Value.Name.Should().Be("My Group");
    }

    [Fact]
    public void CreateGroup_WithNullName_ShouldSucceed()
    {
        var participants = new[] { UserId.New(), UserId.New() };

        var result = Conversation.CreateGroup(null, participants);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().BeNull();
    }

    [Fact]
    public void CreateGroup_WithFewerThanTwoParticipants_ShouldFail()
    {
        var participants = new[] { UserId.New() };

        var result = Conversation.CreateGroup("Only one", participants);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }
}
