namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed record CreateGuildInviteResponse(
    Guid InviteId,
    string Code,
    Guid GuildId,
    Guid CreatorId,
    int? MaxUses,
    int UsesCount,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);
