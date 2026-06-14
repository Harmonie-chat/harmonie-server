using Harmonie.Application.Interfaces.Notifications;

namespace Harmonie.Application.Services.Notifications;

public static class NotificationMessageScopes
{
    public const string Channel = "channel";
    public const string Conversation = "conversation";
}

public sealed class MessageNotificationPayloadFactory
{
    public NotificationDeliveryPayload Create(MessageNotificationContext context)
    {
        var authorName = string.IsNullOrWhiteSpace(context.AuthorDisplayName)
            ? context.AuthorUsername
            : context.AuthorDisplayName;

        object data = context.Target switch
        {
            MessageNotificationTarget.Channel channel => new MessageCreatedChannelNotificationData(
                NotificationMessageScopes.Channel,
                context.MessageId.Value,
                context.AuthorUserId.Value,
                authorName,
                channel.GuildId.Value,
                channel.GuildName,
                channel.ChannelId.Value,
                channel.ChannelName),

            MessageNotificationTarget.Conversation conversation => new MessageCreatedConversationNotificationData(
                NotificationMessageScopes.Conversation,
                context.MessageId.Value,
                context.AuthorUserId.Value,
                authorName,
                conversation.ConversationId.Value,
                conversation.ConversationName),

            _ => throw new InvalidOperationException("Unsupported message notification target")
        };

        return new NotificationDeliveryPayload(NotificationDeliveryPayloadTypes.MessageCreated, data);
    }
}
