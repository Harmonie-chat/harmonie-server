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
            contentResult.Value!,
            TestClock.UtcNow);

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
            contentResult.Value!,
            TestClock.UtcNow);

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
            contentResult.Value!,
            TestClock.UtcNow);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Should().NotBeNull();

        var firstDelete = createResult.Value!.Delete(TestClock.UtcNow.AddMinutes(1));
        var secondDelete = createResult.Value.Delete(TestClock.UtcNow.AddMinutes(2));

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
            createdAtUtc: TestClock.UtcNow,
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
            createdAtUtc: TestClock.UtcNow,
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
            contentResult.Value!,
            TestClock.UtcNow);
        createResult.IsSuccess.Should().BeTrue();

        var message = createResult.Value!;
        message.UpdatedAtUtc.Should().BeNull();

        var newContentResult = MessageContent.Create("updated");
        newContentResult.IsSuccess.Should().BeTrue();

        var updateResult = message.UpdateContent(newContentResult.Value!, TestClock.UtcNow.AddMinutes(1));

        updateResult.IsSuccess.Should().BeTrue();
        message.Content.Should().Be(newContentResult.Value);
        message.UpdatedAtUtc.Should().Be(TestClock.UtcNow.AddMinutes(1));
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
            createdAtUtc: TestClock.UtcNow,
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
            content: null,
            createdAtUtc: TestClock.UtcNow);

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
            contentResult.Value!,
            TestClock.UtcNow);
        createResult.IsSuccess.Should().BeTrue();

        var message = createResult.Value!;
        message.DeletedAtUtc.Should().BeNull();
        message.UpdatedAtUtc.Should().BeNull();

        var deleteResult = message.Delete(TestClock.UtcNow.AddMinutes(1));

        deleteResult.IsSuccess.Should().BeTrue();
        message.DeletedAtUtc.Should().Be(TestClock.UtcNow.AddMinutes(1));
        message.UpdatedAtUtc.Should().Be(TestClock.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Create_WithValidMentions_ShouldIncludeMentionedUserIds()
    {
        var userId = UserId.New();
        var mentionA = UserId.New();
        var mentionB = UserId.New();

        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            userId,
            content: null,
            createdAtUtc: TestClock.UtcNow,
            mentionedUserIds: new[] { mentionA, mentionB });

        result.IsSuccess.Should().BeTrue();
        result.Value!.MentionedUserIds.Should().BeEquivalentTo([mentionA, mentionB]);
    }

    [Fact]
    public void Create_WithNoMentions_ShouldHaveEmptyCollection()
    {
        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            content: null,
            createdAtUtc: TestClock.UtcNow,
            mentionedUserIds: null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MentionedUserIds.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithDuplicateMentionedUserIds_ShouldFail()
    {
        var duplicateId = UserId.New();

        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            content: null,
            createdAtUtc: TestClock.UtcNow,
            mentionedUserIds: new[] { duplicateId, duplicateId });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("distinct");
    }

    [Fact]
    public void Create_WithMoreThanMaxMentions_ShouldFail()
    {
        var ids = Enumerable.Range(0, Message.MaxMentionedUsers + 1)
            .Select(_ => UserId.New())
            .ToArray();

        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            content: null,
            createdAtUtc: TestClock.UtcNow,
            mentionedUserIds: ids);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(Message.MaxMentionedUsers.ToString());
    }

    [Fact]
    public void Create_WithExactlyMaxMentions_ShouldSucceed()
    {
        var ids = Enumerable.Range(0, Message.MaxMentionedUsers)
            .Select(_ => UserId.New())
            .ToArray();

        var result = Message.Create(
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            content: null,
            createdAtUtc: TestClock.UtcNow,
            mentionedUserIds: ids);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MentionedUserIds.Should().HaveCount(Message.MaxMentionedUsers);
    }

    [Fact]
    public void Rehydrate_ShouldDefensiveCopyMentionedUserIds()
    {
        var mutableList = new List<UserId> { UserId.New(), UserId.New() };

        var message = Message.Rehydrate(
            MessageId.New(),
            new MessageScope.Channel(GuildChannelId.New()),
            UserId.New(),
            replyToMessageId: null,
            content: null,
            TestClock.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null,
            mentionedUserIds: mutableList);

        // Verify we got a copy, not the original mutable list
        message.MentionedUserIds.Should().HaveCount(2);
        mutableList.Clear();
        message.MentionedUserIds.Should().HaveCount(2);
    }
}
