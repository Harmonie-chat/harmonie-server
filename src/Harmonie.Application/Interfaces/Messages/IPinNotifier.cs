using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Messages;

public interface IPinNotifier
{
    Task NotifyMessagePinnedInChannelAsync(
        ChannelPinAddedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessagePinnedInConversationAsync(
        ConversationPinAddedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageUnpinnedInChannelAsync(
        ChannelPinRemovedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageUnpinnedInConversationAsync(
        ConversationPinRemovedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record ChannelPinAddedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    UserId PinnedByUserId,
    string PinnedByUsername,
    string? PinnedByDisplayName,
    DateTime PinnedAtUtc);

public sealed record ConversationPinAddedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string? ConversationName,
    string ConversationType,
    UserId PinnedByUserId,
    string PinnedByUsername,
    string? PinnedByDisplayName,
    DateTime PinnedAtUtc);

public sealed record ChannelPinRemovedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    UserId UnpinnedByUserId,
    string UnpinnedByUsername,
    string? UnpinnedByDisplayName,
    DateTime UnpinnedAtUtc);

public sealed record ConversationPinRemovedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string? ConversationName,
    string ConversationType,
    UserId UnpinnedByUserId,
    string UnpinnedByUsername,
    string? UnpinnedByDisplayName,
    DateTime UnpinnedAtUtc);
