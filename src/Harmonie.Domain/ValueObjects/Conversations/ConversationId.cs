using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Conversations;

public sealed record ConversationId : IParsable<ConversationId>
{
    public Guid Value { get; }

    private ConversationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Conversation ID cannot be empty", nameof(value));

        Value = value;
    }

    public static ConversationId New() => new(Guid.NewGuid());

    public static ConversationId From(Guid value) => new(value);

    public static bool TryParse(string? value, out ConversationId? conversationId)
    {
        conversationId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            return false;

        conversationId = new ConversationId(parsed);
        return true;
    }

    public static ConversationId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid ConversationId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out ConversationId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var parsed) || parsed == Guid.Empty)
            return false;

        result = new ConversationId(parsed);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ConversationId conversationId) => conversationId.Value;
}
