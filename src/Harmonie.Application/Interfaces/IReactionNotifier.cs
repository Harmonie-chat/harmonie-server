using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IReactionNotifier
{
    Task NotifyReactionAddedToChannelAsync(
        ChannelReactionAddedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyReactionAddedToConversationAsync(
        ConversationReactionAddedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyReactionRemovedFromChannelAsync(
        ChannelReactionRemovedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyReactionRemovedFromConversationAsync(
        ConversationReactionRemovedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record ChannelReactionAddedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    GuildId GuildId,
    UserId UserId,
    string Emoji);

public sealed record ConversationReactionAddedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    UserId UserId,
    string Emoji);

public sealed record ChannelReactionRemovedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    GuildId GuildId,
    UserId UserId,
    string Emoji);

public sealed record ConversationReactionRemovedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    UserId UserId,
    string Emoji);
