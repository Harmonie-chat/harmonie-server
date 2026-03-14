namespace Harmonie.Application.Features.Conversations.DeleteMessageAttachment;

public sealed class DeleteMessageAttachmentRouteRequest
{
    public string? ConversationId { get; init; }
    public string? MessageId { get; init; }
    public string? AttachmentId { get; init; }
}
