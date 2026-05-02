using FluentAssertions;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
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

    [Fact]
    public void UpdateName_WhenDirectConversation_ShouldFail()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Direct, null, DateTime.UtcNow);

        var result = conversation.UpdateName("Should Fail");

        result.IsFailure.Should().BeTrue();
        conversation.Name.Should().BeNull();
    }

    [Fact]
    public void UpdateName_WithValidName_ShouldSucceed()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Original", DateTime.UtcNow);

        var result = conversation.UpdateName("New Name");

        result.IsSuccess.Should().BeTrue();
        conversation.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateName_WithNull_ShouldSucceed()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Original", DateTime.UtcNow);

        var result = conversation.UpdateName(null);

        result.IsSuccess.Should().BeTrue();
        conversation.Name.Should().BeNull();
    }

    [Fact]
    public void UpdateName_WithEmptyString_ShouldFail()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Original", DateTime.UtcNow);

        var result = conversation.UpdateName("");

        result.IsFailure.Should().BeTrue();
        conversation.Name.Should().Be("Original");
    }

    [Fact]
    public void UpdateName_WithWhitespaceOnly_ShouldFail()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Original", DateTime.UtcNow);

        var result = conversation.UpdateName("   ");

        result.IsFailure.Should().BeTrue();
        conversation.Name.Should().Be("Original");
    }

    [Fact]
    public void UpdateName_WithNameExceedingMaxLength_ShouldFail()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Original", DateTime.UtcNow);

        var result = conversation.UpdateName(new string('a', 101));

        result.IsFailure.Should().BeTrue();
        conversation.Name.Should().Be("Original");
    }

    [Fact]
    public void UpdateName_WithNameAtMaxLength_ShouldSucceed()
    {
        var conversation = Conversation.Rehydrate(ConversationId.New(), ConversationType.Group, "Original", DateTime.UtcNow);

        var result = conversation.UpdateName(new string('a', 100));

        result.IsSuccess.Should().BeTrue();
        conversation.Name.Should().Be(new string('a', 100));
    }
}
