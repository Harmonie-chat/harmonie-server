using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class Guild : Entity<GuildId>
{
    public GuildName Name { get; private set; }

    public UserId OwnerUserId { get; private set; }

    private Guild(
        GuildId id,
        GuildName name,
        UserId ownerUserId,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        Id = id;
        Name = name;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Result<Guild> Create(
        GuildName name,
        UserId ownerUserId)
    {
        if (name is null)
            return Result.Failure<Guild>("Guild name is required");

        if (ownerUserId is null)
            return Result.Failure<Guild>("Owner user ID is required");

        var now = DateTime.UtcNow;
        var guild = new Guild(
            GuildId.New(),
            name,
            ownerUserId,
            now,
            now);

        return Result.Success(guild);
    }

    public static Guild Rehydrate(
        GuildId id,
        GuildName name,
        UserId ownerUserId,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ownerUserId);

        return new Guild(
            id,
            name,
            ownerUserId,
            createdAtUtc,
            updatedAtUtc);
    }
}
