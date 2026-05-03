namespace Harmonie.Application.Features.Conversations.SendMessage;

public sealed record SendMessageRequest(
    string? Content,
    IReadOnlyList<Guid>? AttachmentFileIds = null,
    Guid? ReplyToMessageId = null);
