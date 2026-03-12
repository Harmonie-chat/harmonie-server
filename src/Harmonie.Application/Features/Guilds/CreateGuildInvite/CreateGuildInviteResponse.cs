namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed record CreateGuildInviteResponse(
    string InviteId,
    string Code,
    string GuildId,
    string CreatorId,
    int? MaxUses,
    int UsesCount,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);
