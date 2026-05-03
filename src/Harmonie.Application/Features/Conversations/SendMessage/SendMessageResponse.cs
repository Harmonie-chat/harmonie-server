using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Conversations.SendMessage;

public sealed record SendMessageResponse(
    Guid MessageId,
    Guid ConversationId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    ReplyPreviewDto? ReplyTo,
    DateTime CreatedAtUtc);
