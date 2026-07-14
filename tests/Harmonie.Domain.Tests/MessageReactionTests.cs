using FluentAssertions;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class MessageReactionTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        var messageId = MessageId.New();
        var userId = UserId.New();

        var result = MessageReaction.Create(messageId, userId, "👍", TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.MessageId.Should().Be(messageId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Emoji.Should().Be("👍");
        result.Value.CreatedAtUtc.Should().Be(TestTime.UtcNow);
    }

    [Fact]
    public void Create_WithNullMessageId_ShouldFail()
    {
        var result = MessageReaction.Create(null!, UserId.New(), "👍", TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Message ID is required");
    }

    [Fact]
    public void Create_WithNullUserId_ShouldFail()
    {
        var result = MessageReaction.Create(MessageId.New(), null!, "👍", TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User ID is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidEmoji_ShouldFail(string? emoji)
    {
        var result = MessageReaction.Create(MessageId.New(), UserId.New(), emoji!, TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Emoji is required");
    }

    [Fact]
    public void Rehydrate_WithNullMessageId_ShouldThrow()
    {
        var act = () => MessageReaction.Rehydrate(null!, UserId.New(), "👍", TestTime.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rehydrate_WithNullUserId_ShouldThrow()
    {
        var act = () => MessageReaction.Rehydrate(MessageId.New(), null!, "👍", TestTime.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rehydrate_WithEmptyEmoji_ShouldThrow()
    {
        var act = () => MessageReaction.Rehydrate(MessageId.New(), UserId.New(), "", TestTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rehydrate_ShouldPreserveCreatedAtUtc()
    {
        var createdAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        var reaction = MessageReaction.Rehydrate(MessageId.New(), UserId.New(), "❤️", createdAt);

        reaction.CreatedAtUtc.Should().Be(createdAt);
    }
}
