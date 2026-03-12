namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed record CreateGuildInviteRequest(
    int? MaxUses = null,
    int? ExpiresInHours = null);
