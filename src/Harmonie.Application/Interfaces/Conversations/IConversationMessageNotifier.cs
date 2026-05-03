using Harmonie.Application.Common.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public interface IConversationMessageNotifier
{
    Task NotifyMessageCreatedAsync(
        ConversationMessageCreatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageUpdatedAsync(
        ConversationMessageUpdatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessageDeletedAsync(
        ConversationMessageDeletedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMessagePreviewUpdatedAsync(
        ConversationMessagePreviewUpdatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record ConversationMessageCreatedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string? ConversationName,
    string ConversationType,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    ReplyPreviewDto? ReplyTo,
    DateTime CreatedAtUtc);

public sealed record ConversationMessageUpdatedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string? ConversationName,
    string ConversationType,
    string? Content,
    DateTime UpdatedAtUtc);

public sealed record ConversationMessageDeletedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string? ConversationName,
    string ConversationType);

public sealed record ConversationMessagePreviewUpdatedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    string? ConversationName,
    string ConversationType,
    IReadOnlyList<LinkPreviewDto> Previews);
