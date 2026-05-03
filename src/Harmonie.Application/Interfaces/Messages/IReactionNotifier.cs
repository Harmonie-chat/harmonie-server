using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Messages;

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
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    UserId UserId,
    string Username,
    string? DisplayName,
    string Emoji);

public sealed record ConversationReactionAddedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    UserId UserId,
    string Username,
    string? DisplayName,
    string Emoji);

public sealed record ChannelReactionRemovedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    string ChannelName,
    GuildId GuildId,
    string GuildName,
    UserId UserId,
    string Username,
    string? DisplayName,
    string Emoji);

public sealed record ConversationReactionRemovedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    UserId UserId,
    string Username,
    string? DisplayName,
    string Emoji);
