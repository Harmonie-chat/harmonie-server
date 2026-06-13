using Harmonie.Application.Interfaces.Notifications;

namespace Harmonie.Application.Services.Notifications;

public sealed class MessageNotificationPayloadFactory
{
    private const string DefaultIcon = "/harmonie.png";
    private const string DefaultBadge = "/pwa-icon-192.png";

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
                DefaultIcon,
                DefaultBadge),

            MessageNotificationTarget.Conversation conversation => new NotificationDeliveryPayload(
                authorName,
                body,
                $"/conversations/{conversation.ConversationId.Value}",
                $"message-{context.MessageId.Value}",
                DefaultIcon,
                DefaultBadge),

            _ => throw new InvalidOperationException("Unsupported message notification target")
        };
    }
}
