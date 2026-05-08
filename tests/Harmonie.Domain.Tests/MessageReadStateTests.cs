using FluentAssertions;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class MessageReadStateTests
{
    [Fact]
    public void Create_WithChannelScope_ShouldSucceed()
    {
        var userId = UserId.New();
        var channelId = GuildChannelId.New();
        var messageId = MessageId.New();

        var result = MessageReadState.Create(userId, new MessageScope.Channel(channelId), messageId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.Scope.Should().BeOfType<MessageScope.Channel>();
        ((MessageScope.Channel)result.Value.Scope).ChannelId.Should().Be(channelId);
        result.Value.LastReadMessageId.Should().Be(messageId);
        result.Value.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithConversationScope_ShouldSucceed()
    {
        var userId = UserId.New();
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();

        var result = MessageReadState.Create(userId, new MessageScope.Conversation(conversationId), messageId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.Scope.Should().BeOfType<MessageScope.Conversation>();
        ((MessageScope.Conversation)result.Value.Scope).ConversationId.Should().Be(conversationId);
        result.Value.LastReadMessageId.Should().Be(messageId);
    }

    [Fact]
    public void Create_WithNullUserId_ShouldFail()
    {
        var result = MessageReadState.Create(null!, new MessageScope.Channel(GuildChannelId.New()), MessageId.New());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullScope_ShouldFail()
    {
        var result = MessageReadState.Create(UserId.New(), null!, MessageId.New());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Acknowledge_ShouldUpdateFields()
    {
        var state = MessageReadState.Rehydrate(UserId.New(), new MessageScope.Channel(GuildChannelId.New()), MessageId.New(), DateTime.UtcNow.AddMinutes(-5));
        var newMessageId = MessageId.New();

        state.Acknowledge(newMessageId);

        state.LastReadMessageId.Should().Be(newMessageId);
        state.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Rehydrate_WithNullScope_ShouldThrow()
    {
        var act = () => MessageReadState.Rehydrate(UserId.New(), null!, MessageId.New(), DateTime.UtcNow);
        act.Should().Throw<ArgumentNullException>();
    }
}
