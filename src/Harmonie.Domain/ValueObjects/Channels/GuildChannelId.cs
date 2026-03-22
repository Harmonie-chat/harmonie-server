using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Channels;

/// <summary>
/// Strongly-typed identifier for guild channels.
/// </summary>
public sealed record GuildChannelId : IParsable<GuildChannelId>
{
    public Guid Value { get; }

    private GuildChannelId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guild channel ID cannot be empty", nameof(value));

        Value = value;
    }

    public static GuildChannelId New() => new(Guid.NewGuid());

    public static GuildChannelId From(Guid value) => new(value);

    public static bool TryParse(string? value, out GuildChannelId? channelId)
    {
        channelId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            return false;

        channelId = new GuildChannelId(guid);
        return true;
    }

    public static GuildChannelId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid GuildChannelId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out GuildChannelId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var guid) || guid == Guid.Empty)
            return false;

        result = new GuildChannelId(guid);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GuildChannelId channelId) => channelId.Value;
}
