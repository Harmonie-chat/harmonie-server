using FluentAssertions;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ConversationReadStateTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        var userId = UserId.New();
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();

        var result = ConversationReadState.Create(userId, conversationId, messageId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.ConversationId.Should().Be(conversationId);
        result.Value.LastReadMessageId.Should().Be(messageId);
        result.Value.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithNullUserId_ShouldFail()
    {
        var result = ConversationReadState.Create(null!, ConversationId.New(), MessageId.New());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User ID is required");
    }

    [Fact]
    public void Create_WithNullConversationId_ShouldFail()
    {
        var result = ConversationReadState.Create(UserId.New(), null!, MessageId.New());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Conversation ID is required");
    }

    [Fact]
    public void Create_WithNullMessageId_ShouldFail()
    {
        var result = ConversationReadState.Create(UserId.New(), ConversationId.New(), null!);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Last read message ID is required");
    }

    [Fact]
    public void Acknowledge_ShouldUpdateFields()
    {
        var state = ConversationReadState.Rehydrate(UserId.New(), ConversationId.New(), MessageId.New(), DateTime.UtcNow.AddMinutes(-5));
        var newMessageId = MessageId.New();
        var now = DateTime.UtcNow;

        state.Acknowledge(newMessageId, now);

        state.LastReadMessageId.Should().Be(newMessageId);
        state.ReadAtUtc.Should().Be(now);
    }
}
