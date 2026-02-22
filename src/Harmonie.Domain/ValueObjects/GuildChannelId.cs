namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for guild channels.
/// </summary>
public sealed record GuildChannelId
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

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GuildChannelId channelId) => channelId.Value;
}
