namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for guild invite entities.
/// </summary>
public sealed record GuildInviteId
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

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GuildInviteId guildInviteId) => guildInviteId.Value;
}
