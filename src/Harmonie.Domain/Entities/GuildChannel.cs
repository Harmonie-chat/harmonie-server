using Harmonie.Domain.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class GuildChannel : Entity<GuildChannelId>
{
    public GuildId GuildId { get; private set; }

    public string Name { get; private set; }

    public GuildChannelType Type { get; private set; }

    public bool IsDefault { get; private set; }

    public int Position { get; private set; }

    private GuildChannel(
        GuildChannelId id,
        GuildId guildId,
        string name,
        GuildChannelType type,
        bool isDefault,
        int position,
        DateTime createdAtUtc)
    {
        Id = id;
        GuildId = guildId;
        Name = name;
        Type = type;
        IsDefault = isDefault;
        Position = position;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<GuildChannel> Create(
        GuildId guildId,
        string? name,
        GuildChannelType type,
        bool isDefault,
        int position)
    {
        if (guildId is null)
            return Result.Failure<GuildChannel>("Guild ID is required");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<GuildChannel>("Channel name is required");

        var normalizedName = name.Trim();
        if (normalizedName.Length > 100)
            return Result.Failure<GuildChannel>("Channel name cannot exceed 100 characters");

        if (!Enum.IsDefined(type))
            return Result.Failure<GuildChannel>("Channel type is invalid");

        if (position < 0)
            return Result.Failure<GuildChannel>("Channel position cannot be negative");

        return Result.Success(new GuildChannel(
            GuildChannelId.New(),
            guildId,
            normalizedName,
            type,
            isDefault,
            position,
            DateTime.UtcNow));
    }

    public static GuildChannel Rehydrate(
        GuildChannelId id,
        GuildId guildId,
        string name,
        GuildChannelType type,
        bool isDefault,
        int position,
        DateTime createdAtUtc)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        if (guildId is null)
            throw new ArgumentNullException(nameof(guildId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name is required.", nameof(name));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), "Channel type is invalid.");
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position), "Channel position cannot be negative.");

        return new GuildChannel(
            id,
            guildId,
            name,
            type,
            isDefault,
            position,
            createdAtUtc);
    }
}
