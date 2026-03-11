namespace Harmonie.Domain.ValueObjects;

public sealed record MessageId
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

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(MessageId messageId) => messageId.Value;
}
