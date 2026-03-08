namespace Harmonie.Domain.ValueObjects;

public sealed record UploadedFileId
{
    public Guid Value { get; }

    private UploadedFileId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Uploaded file ID cannot be empty", nameof(value));

        Value = value;
    }

    public static UploadedFileId New() => new(Guid.NewGuid());

    public static UploadedFileId From(Guid value) => new(value);

    public static bool TryParse(string value, out UploadedFileId? uploadedFileId)
    {
        uploadedFileId = null;
        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            return false;

        uploadedFileId = new UploadedFileId(guid);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(UploadedFileId uploadedFileId) => uploadedFileId.Value;
}
