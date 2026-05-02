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
    public void CreateForChannel_WithValidInputs_ShouldSucceed()
    {
        var userId = UserId.New();
        var channelId = GuildChannelId.New();
        var messageId = MessageId.New();

        var result = MessageReadState.CreateForChannel(userId, channelId, messageId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.ChannelId.Should().Be(channelId);
        result.Value.LastReadMessageId.Should().Be(messageId);
        result.Value.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateForConversation_WithValidInputs_ShouldSucceed()
    {
        var userId = UserId.New();
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();

        var result = MessageReadState.CreateForConversation(userId, conversationId, messageId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.ConversationId.Should().Be(conversationId);
        result.Value.LastReadMessageId.Should().Be(messageId);
    }

    [Fact]
    public void CreateForChannel_WithNullUserId_ShouldFail()
    {
        var result = MessageReadState.CreateForChannel(null!, GuildChannelId.New(), MessageId.New());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateForConversation_WithNullConversationId_ShouldFail()
    {
        var result = MessageReadState.CreateForConversation(UserId.New(), null!, MessageId.New());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Acknowledge_ShouldUpdateFields()
    {
        var state = MessageReadState.Rehydrate(UserId.New(), GuildChannelId.New(), conversationId: null, MessageId.New(), DateTime.UtcNow.AddMinutes(-5));
        var newMessageId = MessageId.New();

        state.Acknowledge(newMessageId);

        state.LastReadMessageId.Should().Be(newMessageId);
        state.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Rehydrate_WithBothIdsNull_ShouldThrow()
    {
        var act = () => MessageReadState.Rehydrate(UserId.New(), channelId: null, conversationId: null, MessageId.New(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }
}
