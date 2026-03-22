using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Messages;

public sealed record MessageId : IParsable<MessageId>
{
    public Guid Value { get; }

    private MessageId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Message ID cannot be empty", nameof(value));

        Value = value;
    }

    public static MessageId New() => new(Guid.NewGuid());

    public static MessageId From(Guid value) => new(value);

    public static bool TryParse(string? value, out MessageId? messageId)
    {
        messageId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            return false;

        messageId = new MessageId(parsed);
        return true;
    }

    public static MessageId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid MessageId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out MessageId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var parsed) || parsed == Guid.Empty)
            return false;

        result = new MessageId(parsed);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(MessageId messageId) => messageId.Value;
}
