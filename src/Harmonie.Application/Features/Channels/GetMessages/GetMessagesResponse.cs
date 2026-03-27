using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed record GetMessagesResponse(
    Guid ChannelId,
    IReadOnlyList<GetMessagesItemResponse> Items,
    string? NextCursor,
    Guid? LastReadMessageId);

public sealed record GetMessagesItemResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    IReadOnlyList<MessageReactionDto> Reactions,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
