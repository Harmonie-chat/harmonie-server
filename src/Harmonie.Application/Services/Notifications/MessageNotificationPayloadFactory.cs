using Harmonie.Application.Interfaces.Notifications;
using Microsoft.Extensions.Options;

namespace Harmonie.Application.Services.Notifications;

public sealed class MessageNotificationPayloadFactory
{
    private readonly PushNotificationOptions _options;

    public MessageNotificationPayloadFactory(IOptions<PushNotificationOptions> options)
    {
        _options = options.Value;
    }

    public NotificationDeliveryPayload Create(MessageNotificationContext context)
    {
        var authorName = string.IsNullOrWhiteSpace(context.AuthorDisplayName)
            ? context.AuthorUsername
            : context.AuthorDisplayName;
        var body = string.IsNullOrWhiteSpace(context.Content) ? "New message" : context.Content;

        return context.Target switch
        {
            MessageNotificationTarget.Channel channel => new NotificationDeliveryPayload(
                $"{authorName} | {channel.ChannelName}",
                body,
                $"/guilds/{channel.GuildId.Value}/channels/{channel.ChannelId.Value}",
                $"message-{context.MessageId.Value}",
                _options.Icon,
                _options.Badge),

            MessageNotificationTarget.Conversation conversation => new NotificationDeliveryPayload(
                authorName,
                body,
                $"/conversations/{conversation.ConversationId.Value}",
                $"message-{context.MessageId.Value}",
                _options.Icon,
                _options.Badge),

            _ => throw new InvalidOperationException("Unsupported message notification target")
        };
    }
}
