using Harmonie.Application.Common.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Messages;

public interface IPinnedMessageRepository
{
    Task AddAsync(
        PinnedMessage pinnedMessage,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PinnedMessageSummary>> GetPinnedMessagesAsync(
        GuildChannelId channelId,
        UserId callerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PinnedMessageSummary>> GetPinnedMessagesAsync(
        ConversationId conversationId,
        UserId callerId,
        CancellationToken cancellationToken = default);
}

public sealed record PinnedMessageSummary(
    Guid MessageId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    IReadOnlyList<MessageReactionDto> Reactions,
    IReadOnlyList<LinkPreviewDto>? LinkPreviews,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid PinnedByUserId,
    DateTime PinnedAtUtc);
