using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class MessageNotificationPayloadFactoryTests
{
    private readonly MessageNotificationPayloadFactory _factory = new();

    [Fact]
    public void Create_ForChannelMessage_ShouldBuildMessageCreatedDataPayload()
    {
        var guildId = GuildId.New();
        var channelId = GuildChannelId.New();
        var messageId = MessageId.New();
        var authorId = UserId.New();
        var context = new MessageNotificationContext(
            messageId,
            authorId,
            "alice",
            "Alice",
            new MessageNotificationTarget.Channel(guildId, "Harmonie", channelId, "général"),
            new HashSet<UserId>(),
            new HashSet<UserId>());

        var payload = _factory.Create(context);

        payload.Type.Should().Be(NotificationDeliveryPayloadTypes.MessageCreated);
        payload.Data.Should().BeOfType<MessageCreatedChannelNotificationData>()
            .Which.Should().Be(new MessageCreatedChannelNotificationData(
                NotificationMessageScopes.Channel,
                messageId.Value,
                authorId.Value,
                "Alice",
                guildId.Value,
                "Harmonie",
                channelId.Value,
                "général"));
    }

    [Fact]
    public void Create_ForConversationMessage_ShouldBuildMessageCreatedDataPayload()
    {
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();
        var authorId = UserId.New();
        var context = new MessageNotificationContext(
            messageId,
            authorId,
            "alice",
            null,
            new MessageNotificationTarget.Conversation(conversationId, ConversationType.Direct, null),
            new HashSet<UserId>(),
            new HashSet<UserId>());

        var payload = _factory.Create(context);

        payload.Type.Should().Be(NotificationDeliveryPayloadTypes.MessageCreated);
        payload.Data.Should().BeOfType<MessageCreatedConversationNotificationData>()
            .Which.Should().Be(new MessageCreatedConversationNotificationData(
                NotificationMessageScopes.Conversation,
                messageId.Value,
                authorId.Value,
                "alice",
                conversationId.Value,
                null));
    }
}
