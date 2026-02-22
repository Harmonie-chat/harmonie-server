using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface ITextChannelNotifier
{
    Task NotifyMessageCreatedAsync(
        TextChannelMessageCreatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record TextChannelMessageCreatedNotification(
    ChannelMessageId MessageId,
    GuildChannelId ChannelId,
    UserId AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
