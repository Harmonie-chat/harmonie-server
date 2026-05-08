using FluentAssertions;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class MessageTests
{
    [Fact]
    public void Create_WithChannelScope_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello channel");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            contentResult.Value!);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Scope.Should().BeOfType<MessageScope.Channel>();
        result.Value.DeletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_WithConversationScope_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello there");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var result = Message.Create(
            new MessageScope.Conversation(ConversationId.New()),
            UserId.New(),
            contentResult.Value!);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Scope.Should().BeOfType<MessageScope.Conversation>();
        result.Value.DeletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Delete_WhenMessageAlreadyDeleted_ShouldFail()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var createResult = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
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
    public void Rehydrate_WithChannelScope_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var channelId = GuildChannelId.New();
        var scope = new MessageScope.Channel(channelId);

        var message = Message.Rehydrate(
            MessageId.New(),
            scope,
            authorUserId: UserId.New(),
            replyToMessageId: null,
            content: contentResult.Value!,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);

        message.Scope.Should().BeOfType<MessageScope.Channel>();
        ((MessageScope.Channel)message.Scope).ChannelId.Should().Be(channelId);
    }

    [Fact]
    public void Rehydrate_WithConversationScope_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var conversationId = ConversationId.New();
        var scope = new MessageScope.Conversation(conversationId);

        var message = Message.Rehydrate(
            MessageId.New(),
            scope,
            authorUserId: UserId.New(),
            replyToMessageId: null,
            content: contentResult.Value!,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);

        message.Scope.Should().BeOfType<MessageScope.Conversation>();
        ((MessageScope.Conversation)message.Scope).ConversationId.Should().Be(conversationId);
    }

    [Fact]
    public void UpdateContent_WithValidContent_ShouldUpdateContentAndTimestamp()
    {
        var contentResult = MessageContent.Create("original");
        contentResult.IsSuccess.Should().BeTrue();

        var createResult = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            contentResult.Value!);
        createResult.IsSuccess.Should().BeTrue();

        var message = createResult.Value!;
        message.UpdatedAtUtc.Should().BeNull();

        var newContentResult = MessageContent.Create("updated");
        newContentResult.IsSuccess.Should().BeTrue();

        var updateResult = message.UpdateContent(newContentResult.Value!);

        updateResult.IsSuccess.Should().BeTrue();
        message.Content.Should().Be(newContentResult.Value);
        message.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Rehydrate_WithNullScope_ShouldThrow()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();

        var act = () => Message.Rehydrate(
            MessageId.New(),
            scope: null!,
            authorUserId: UserId.New(),
            replyToMessageId: null,
            content: contentResult.Value!,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullContent_ShouldSucceed()
    {
        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            content: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Content.Should().BeNull();
    }

    [Fact]
    public void Delete_WhenNotDeleted_ShouldSetDeletedAtAndUpdatedAt()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();

        var createResult = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            contentResult.Value!);
        createResult.IsSuccess.Should().BeTrue();

        var message = createResult.Value!;
        message.DeletedAtUtc.Should().BeNull();
        message.UpdatedAtUtc.Should().BeNull();

        var deleteResult = message.Delete();

        deleteResult.IsSuccess.Should().BeTrue();
        message.DeletedAtUtc.Should().NotBeNull();
        message.UpdatedAtUtc.Should().NotBeNull();
    }
}
