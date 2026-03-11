using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class Guild : Entity<GuildId>
{
    public GuildName Name { get; private set; }

    public UserId OwnerUserId { get; private set; }

    public UploadedFileId? IconFileId { get; private set; }

    public string? IconColor { get; private set; }

    public string? IconName { get; private set; }

    public string? IconBg { get; private set; }

    private Guild(
        GuildId id,
        GuildName name,
        UserId ownerUserId,
        UploadedFileId? iconFileId,
        string? iconColor,
        string? iconName,
        string? iconBg,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        Id = id;
        Name = name;
        OwnerUserId = ownerUserId;
        IconFileId = iconFileId;
        IconColor = iconColor;
        IconName = iconName;
        IconBg = iconBg;
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
            iconFileId: null,
            iconColor: null,
            iconName: null,
            iconBg: null,
            now,
            now);

        return Result.Success(guild);
    }

    public static Guild Rehydrate(
        GuildId id,
        GuildName name,
        UserId ownerUserId,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        UploadedFileId? iconFileId = null,
        string? iconColor = null,
        string? iconName = null,
        string? iconBg = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ownerUserId);

        return new Guild(
            id,
            name,
            ownerUserId,
            iconFileId,
            iconColor,
            iconName,
            iconBg,
            createdAtUtc,
            updatedAtUtc);
    }

    public Result UpdateName(GuildName newName)
    {
        if (Name == newName)
            return Result.Success();

        Name = newName;
        MarkAsUpdated();

        return Result.Success();
    }

    public Result UpdateIconFile(UploadedFileId? iconFileId)
    {
        IconFileId = iconFileId;
        MarkAsUpdated();

        return Result.Success();
    }

    public Result UpdateIconColor(string? iconColor)
    {
        if (iconColor?.Length > 50)
            return Result.Failure("Guild icon color is too long");

        IconColor = iconColor;
        MarkAsUpdated();

        return Result.Success();
    }

    public Result UpdateIconName(string? iconName)
    {
        if (iconName?.Length > 50)
            return Result.Failure("Guild icon name is too long");

        IconName = iconName;
        MarkAsUpdated();

        return Result.Success();
    }

    public Result UpdateIconBg(string? iconBg)
    {
        if (iconBg?.Length > 50)
            return Result.Failure("Guild icon background is too long");

        IconBg = iconBg;
        MarkAsUpdated();

        return Result.Success();
    }
}
