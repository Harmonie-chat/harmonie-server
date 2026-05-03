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
            replyToMessageId: null,
            content: contentResult.Value!,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateContent_WithValidContent_ShouldUpdateContentAndTimestamp()
    {
        var contentResult = MessageContent.Create("original");
        contentResult.IsSuccess.Should().BeTrue();

        var createResult = Message.CreateForChannel(
            GuildChannelId.New(),
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
    public void Rehydrate_WhenBothParentsPresent_ShouldThrow()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();

        var act = () => Message.Rehydrate(
            MessageId.New(),
            channelId: GuildChannelId.New(),
            conversationId: ConversationId.New(),
            authorUserId: UserId.New(),
            replyToMessageId: null,
            content: contentResult.Value!,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateForChannel_WithNullContentAndOneAttachment_ShouldSucceedWithNullContent()
    {
        var attachment = new MessageAttachment(
            UploadedFileId.New(),
            "photo.png",
            "image/png",
            1024);

        var result = Message.CreateForChannel(
            GuildChannelId.New(),
            UserId.New(),
            content: null,
            attachments: [attachment]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Content.Should().BeNull();
        result.Value.Attachments.Should().ContainSingle();
    }

    [Fact]
    public void CreateForChannel_WithNullContentAndNoAttachments_ShouldFail()
    {
        var result = Message.CreateForChannel(
            GuildChannelId.New(),
            UserId.New(),
            content: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("content or at least one attachment");
    }

    [Fact]
    public void Delete_WhenNotDeleted_ShouldSetDeletedAtAndUpdatedAt()
    {
        var contentResult = MessageContent.Create("hello");
        contentResult.IsSuccess.Should().BeTrue();

        var createResult = Message.CreateForChannel(
            GuildChannelId.New(),
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
