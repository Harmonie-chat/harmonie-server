using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed record EditMessageResponse(
    Guid MessageId,
    Guid ChannelId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
