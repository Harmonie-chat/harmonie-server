namespace Harmonie.Infrastructure.Rows;

internal sealed class MessageAttachmentRow
{
    public Guid MessageId { get; init; }
    public int Position { get; init; }
    public Guid UploadedFileId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}
