using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Uploads;

public sealed record UploadedFileId : IParsable<UploadedFileId>
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

    public static UploadedFileId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid UploadedFileId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out UploadedFileId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var guid) || guid == Guid.Empty)
            return false;

        result = new UploadedFileId(guid);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(UploadedFileId uploadedFileId) => uploadedFileId.Value;
}
