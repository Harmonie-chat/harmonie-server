using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Guilds;

/// <summary>
/// Strongly-typed identifier for guild entities.
/// </summary>
public sealed record GuildId : IParsable<GuildId>
{
    public Guid Value { get; }

    private GuildId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guild ID cannot be empty", nameof(value));

        Value = value;
    }

    public static GuildId New() => new(Guid.NewGuid());

    public static GuildId From(Guid value) => new(value);

    public static bool TryParse(string? value, out GuildId? guildId)
    {
        guildId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            return false;

        guildId = new GuildId(guid);
        return true;
    }

    public static GuildId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid GuildId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out GuildId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var guid) || guid == Guid.Empty)
            return false;

        result = new GuildId(guid);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GuildId guildId) => guildId.Value;
}
