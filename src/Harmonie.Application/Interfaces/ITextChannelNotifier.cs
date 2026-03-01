using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface ITextChannelNotifier
{
    Task NotifyMessageCreatedAsync(
        TextChannelMessageCreatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageUpdatedAsync(
        TextChannelMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageDeletedAsync(
        TextChannelMessageDeletedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record TextChannelMessageCreatedNotification(
    ChannelMessageId MessageId,
    GuildChannelId ChannelId,
    UserId AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);

public sealed record TextChannelMessageUpdatedNotification(
    ChannelMessageId MessageId,
    GuildChannelId ChannelId,
    string Content,
    DateTime UpdatedAtUtc);

public sealed record TextChannelMessageDeletedNotification(
    ChannelMessageId MessageId,
    GuildChannelId ChannelId);
