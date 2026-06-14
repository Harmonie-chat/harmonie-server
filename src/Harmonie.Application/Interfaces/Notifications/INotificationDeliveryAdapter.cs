namespace Harmonie.Application.Interfaces.Notifications;

public enum NotificationDeliveryResultStatus
{
    Succeeded,
    InvalidDevice,
    TransientFailure,
    PermanentFailure
}

public static class NotificationDeliveryPayloadTypes
{
    public const string MessageCreated = "message.created";
}

public sealed record NotificationDeliveryPayload(
    string Type,
    object Data);

public sealed record MessageCreatedChannelNotificationData(
    string Scope,
    Guid MessageId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    Guid GuildId,
    string GuildName,
    Guid ChannelId,
    string ChannelName);

public sealed record MessageCreatedConversationNotificationData(
    string Scope,
    Guid MessageId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    Guid ConversationId,
    string? ConversationName);

public sealed record NotificationDeliveryResult(
    Guid DeviceId,
    NotificationDeliveryResultStatus Status,
    string? Error = null);

public interface INotificationDeliveryAdapter
{
    string Platform { get; }

    Task<IReadOnlyList<NotificationDeliveryResult>> SendAsync(
        NotificationDeliveryPayload payload,
        IReadOnlyList<NotificationDevice> devices,
        CancellationToken cancellationToken = default);
}
