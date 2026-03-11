using FluentAssertions;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class MessageTests
{
    [Fact]
    public void CreateForChannel_WithValidInput_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello channel");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var result = Message.CreateForChannel(
            GuildChannelId.New(),
            UserId.New(),
            contentResult.Value!);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ChannelId.Should().NotBeNull();
        result.Value.ConversationId.Should().BeNull();
        result.Value.DeletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void CreateForConversation_WithValidInput_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello there");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var result = Message.CreateForConversation(
            ConversationId.New(),
            UserId.New(),
            contentResult.Value!);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ChannelId.Should().BeNull();
        result.Value.ConversationId.Should().NotBeNull();
        result.Value.DeletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Delete_WhenMessageAlreadyDeleted_ShouldFail()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var createResult = Message.CreateForChannel(
            GuildChannelId.New(),
            UserId.New(),
            contentResult.Value!);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Should().NotBeNull();

        var firstDelete = createResult.Value!.Delete();
        var secondDelete = createResult.Value.Delete();

        firstDelete.IsSuccess.Should().BeTrue();
        secondDelete.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Rehydrate_WhenBothParentsMissing_ShouldThrow()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var act = () => Message.Rehydrate(
            MessageId.New(),
            channelId: null,
            conversationId: null,
            authorUserId: UserId.New(),
            content: contentResult.Value!,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);

        act.Should().Throw<ArgumentException>();
    }
}
