using FluentAssertions;
using Harmonie.Domain.Entities.Channels;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ChannelReadStateTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        var userId = UserId.New();
        var channelId = GuildChannelId.New();
        var messageId = MessageId.New();

        var result = ChannelReadState.Create(userId, channelId, messageId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.ChannelId.Should().Be(channelId);
        result.Value.LastReadMessageId.Should().Be(messageId);
        result.Value.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithNullUserId_ShouldFail()
    {
        var result = ChannelReadState.Create(null!, GuildChannelId.New(), MessageId.New());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User ID is required");
    }

    [Fact]
    public void Create_WithNullChannelId_ShouldFail()
    {
        var result = ChannelReadState.Create(UserId.New(), null!, MessageId.New());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Channel ID is required");
    }

    [Fact]
    public void Create_WithNullMessageId_ShouldFail()
    {
        var result = ChannelReadState.Create(UserId.New(), GuildChannelId.New(), null!);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Last read message ID is required");
    }

    [Fact]
    public void Acknowledge_ShouldUpdateFields()
    {
        var state = ChannelReadState.Rehydrate(UserId.New(), GuildChannelId.New(), MessageId.New(), DateTime.UtcNow.AddMinutes(-5));
        var newMessageId = MessageId.New();
        var now = DateTime.UtcNow;

        state.Acknowledge(newMessageId, now);

        state.LastReadMessageId.Should().Be(newMessageId);
        state.ReadAtUtc.Should().Be(now);
    }
}
