namespace Harmonie.Application.Features.Guilds.RevokeInvite;

public sealed class RevokeInviteRouteRequest
{
    public string? GuildId { get; init; }
    public string? InviteCode { get; init; }
}
