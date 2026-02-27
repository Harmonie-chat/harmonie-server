using Harmonie.Domain.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class GuildMember
{
    public GuildId GuildId { get; }

    public UserId UserId { get; }

    public GuildRole Role { get; }

    public DateTime JoinedAtUtc { get; }

    public UserId? InvitedByUserId { get; }

    private GuildMember(
        GuildId guildId,
        UserId userId,
        GuildRole role,
        DateTime joinedAtUtc,
        UserId? invitedByUserId)
    {
        GuildId = guildId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
        InvitedByUserId = invitedByUserId;
    }

    public static Result<GuildMember> Create(
        GuildId guildId,
        UserId userId,
        GuildRole role,
        UserId? invitedByUserId)
    {
        if (guildId is null)
            return Result.Failure<GuildMember>("Guild ID is required");

        if (userId is null)
            return Result.Failure<GuildMember>("User ID is required");

        if (!Enum.IsDefined(role))
            return Result.Failure<GuildMember>("Guild role is invalid");

        if (role == GuildRole.Admin && invitedByUserId is not null)
            return Result.Failure<GuildMember>("Admin membership cannot have an inviter");

        return Result.Success(new GuildMember(
            guildId,
            userId,
            role,
            DateTime.UtcNow,
            invitedByUserId));
    }

    public static GuildMember Rehydrate(
        GuildId guildId,
        UserId userId,
        GuildRole role,
        DateTime joinedAtUtc,
        UserId? invitedByUserId)
    {
        ArgumentNullException.ThrowIfNull(guildId);
        ArgumentNullException.ThrowIfNull(userId);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), "Guild role is invalid.");

        return new GuildMember(
            guildId,
            userId,
            role,
            joinedAtUtc,
            invitedByUserId);
    }
}
