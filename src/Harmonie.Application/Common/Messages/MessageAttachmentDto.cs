using Harmonie.Domain.Entities.Messages;

namespace Harmonie.Application.Common.Messages;

public sealed record MessageAttachmentDto(
    Guid FileId,
    string FileName,
    string ContentType,
    long SizeBytes)
{
    public static MessageAttachmentDto FromDomain(MessageAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new MessageAttachmentDto(
            attachment.FileId.Value,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes);
    }
}
