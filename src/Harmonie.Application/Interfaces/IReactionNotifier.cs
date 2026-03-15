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
}

public sealed record ChannelReactionAddedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    UserId UserId,
    string Emoji);

public sealed record ConversationReactionAddedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    UserId UserId,
    string Emoji);
