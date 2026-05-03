namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed record SendMessageRequest(
    string? Content,
    IReadOnlyList<Guid>? AttachmentFileIds = null,
    Guid? ReplyToMessageId = null);
