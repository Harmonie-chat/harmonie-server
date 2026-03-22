using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Guilds;

/// <summary>
/// Strongly-typed identifier for guild invite entities.
/// </summary>
public sealed record GuildInviteId : IParsable<GuildInviteId>
{
    public Guid Value { get; }

    private GuildInviteId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guild invite ID cannot be empty", nameof(value));

        Value = value;
    }

    public static GuildInviteId New() => new(Guid.NewGuid());

    public static GuildInviteId From(Guid value) => new(value);

    public static bool TryParse(string? value, out GuildInviteId? guildInviteId)
    {
        guildInviteId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            return false;

        guildInviteId = new GuildInviteId(guid);
        return true;
    }

    public static GuildInviteId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid GuildInviteId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out GuildInviteId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var guid) || guid == Guid.Empty)
            return false;

        result = new GuildInviteId(guid);
        return true;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GuildInviteId guildInviteId) => guildInviteId.Value;
}
