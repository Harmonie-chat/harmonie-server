using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Guilds;

public sealed class GuildBan
{
    public GuildId GuildId { get; }

    public UserId UserId { get; }

    public string? Reason { get; }

    public UserId BannedBy { get; }

    public DateTime CreatedAtUtc { get; }

    private GuildBan(
        GuildId guildId,
        UserId userId,
        string? reason,
        UserId bannedBy,
        DateTime createdAtUtc)
    {
        GuildId = guildId;
        UserId = userId;
        Reason = reason;
        BannedBy = bannedBy;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<GuildBan> Create(
        GuildId guildId,
        UserId userId,
        string? reason,
        UserId bannedBy,
        DateTime createdAtUtc)
    {
        if (guildId is null)
            return Result.Failure<GuildBan>("Guild ID is required");

        if (userId is null)
            return Result.Failure<GuildBan>("User ID is required");

        if (bannedBy is null)
            return Result.Failure<GuildBan>("Banned by user ID is required");

        if (reason is not null && reason.Length > 512)
            return Result.Failure<GuildBan>("Reason cannot exceed 512 characters");

        return Result.Success(new GuildBan(
            guildId,
            userId,
            reason,
            bannedBy,
            createdAtUtc));
    }

    public static GuildBan Rehydrate(
        GuildId guildId,
        UserId userId,
        string? reason,
        UserId bannedBy,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(guildId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(bannedBy);

        return new GuildBan(
            guildId,
            userId,
            reason,
            bannedBy,
            createdAtUtc);
    }
}
