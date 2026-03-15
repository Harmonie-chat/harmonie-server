using Harmonie.Application.Common;
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
    MessageId MessageId,
    GuildChannelId ChannelId,
    GuildId GuildId,
    UserId AuthorUserId,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public sealed record TextChannelMessageUpdatedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    GuildId GuildId,
    string Content,
    DateTime UpdatedAtUtc);

public sealed record TextChannelMessageDeletedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    GuildId GuildId);
