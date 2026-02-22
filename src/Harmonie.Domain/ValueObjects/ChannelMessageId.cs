namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for channel messages.
/// </summary>
public sealed record ChannelMessageId
{
    public Guid Value { get; }

    private ChannelMessageId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Channel message ID cannot be empty", nameof(value));

        Value = value;
    }

    public static ChannelMessageId New() => new(Guid.NewGuid());

    public static ChannelMessageId From(Guid value) => new(value);

    public static bool TryParse(string? value, out ChannelMessageId? messageId)
    {
        messageId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            return false;

        messageId = new ChannelMessageId(parsed);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ChannelMessageId messageId) => messageId.Value;
}
