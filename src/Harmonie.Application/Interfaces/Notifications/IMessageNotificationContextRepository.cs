using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Notifications;

public abstract record MessageNotificationTarget
{
    private MessageNotificationTarget() { }

    public sealed record Channel(GuildId GuildId, string GuildName, GuildChannelId ChannelId, string ChannelName) : MessageNotificationTarget;

    public sealed record Conversation(ConversationId ConversationId, ConversationType ConversationType, string? ConversationName) : MessageNotificationTarget;
}

public sealed record MessageNotificationContext(
    MessageId MessageId,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    MessageNotificationTarget Target,
    IReadOnlySet<UserId> CandidateRecipientUserIds,
    IReadOnlySet<UserId> MentionedUserIds);

public interface IMessageNotificationContextRepository
{
    Task<MessageNotificationContext?> GetAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);
}
