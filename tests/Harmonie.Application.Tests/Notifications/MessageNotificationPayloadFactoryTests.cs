using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class MessageNotificationPayloadFactoryTests
{
    private readonly MessageNotificationPayloadFactory _factory = new(Options.Create(new PushNotificationOptions
    {
        Icon = "/custom-icon.png",
        Badge = "/custom-badge.png"
    }));

    [Fact]
    public void Create_ForChannelMessage_ShouldBuildServiceWorkerPayload()
    {
        var guildId = GuildId.New();
        var channelId = GuildChannelId.New();
        var messageId = MessageId.New();
        var context = new MessageNotificationContext(
            messageId,
            UserId.New(),
            "alice",
            "Alice",
            "Salut !",
            new MessageNotificationTarget.Channel(guildId, channelId, "général"),
            new HashSet<UserId>());

        var payload = _factory.Create(context);

        payload.Title.Should().Be("Alice | général");
        payload.Body.Should().Be("Salut !");
        payload.TargetUrl.Should().Be($"/guilds/{guildId.Value}/channels/{channelId.Value}");
        payload.Tag.Should().Be($"message-{messageId.Value}");
        payload.Icon.Should().Be("/custom-icon.png");
        payload.Badge.Should().Be("/custom-badge.png");
    }

    [Fact]
    public void Create_ForConversationMessage_ShouldBuildServiceWorkerPayload()
    {
        var conversationId = ConversationId.New();
        var messageId = MessageId.New();
        var context = new MessageNotificationContext(
            messageId,
            UserId.New(),
            "alice",
            null,
            "Salut !",
            new MessageNotificationTarget.Conversation(conversationId),
            new HashSet<UserId>());

        var payload = _factory.Create(context);

        payload.Title.Should().Be("alice");
        payload.Body.Should().Be("Salut !");
        payload.TargetUrl.Should().Be($"/conversations/{conversationId.Value}");
        payload.Tag.Should().Be($"message-{messageId.Value}");
        payload.Icon.Should().Be("/custom-icon.png");
        payload.Badge.Should().Be("/custom-badge.png");
    }
}
