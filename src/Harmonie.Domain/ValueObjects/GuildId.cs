namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for guild entities.
/// </summary>
public sealed record GuildId
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

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GuildId guildId) => guildId.Value;
}
