namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed class UpdateMemberRoleRouteRequest
{
    public string? GuildId { get; init; }
    public string? UserId { get; init; }
}
