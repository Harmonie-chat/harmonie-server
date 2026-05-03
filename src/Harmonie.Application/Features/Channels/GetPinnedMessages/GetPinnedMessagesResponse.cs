using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Channels.GetPinnedMessages;

public sealed record GetPinnedMessagesResponse(
    Guid ChannelId,
    IReadOnlyList<GetPinnedMessagesItemResponse> Items,
    string? NextCursor);

public sealed record GetPinnedMessagesItemResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid PinnedByUserId,
    DateTime PinnedAtUtc);
