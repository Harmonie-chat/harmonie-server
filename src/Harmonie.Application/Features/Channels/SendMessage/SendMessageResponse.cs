using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed record SendMessageResponse(
    Guid MessageId,
    Guid ChannelId,
    Guid AuthorUserId,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc);
