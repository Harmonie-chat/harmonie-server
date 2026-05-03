using Harmonie.Application.Common.Messages;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Channels;

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

    Task NotifyMessagePreviewUpdatedAsync(
        TextChannelMessagePreviewUpdatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record TextChannelMessageCreatedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public sealed record TextChannelMessageUpdatedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    string? Content,
    DateTime UpdatedAtUtc);

public sealed record TextChannelMessageDeletedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName);

public sealed record TextChannelMessagePreviewUpdatedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    IReadOnlyList<LinkPreviewDto> Previews);
