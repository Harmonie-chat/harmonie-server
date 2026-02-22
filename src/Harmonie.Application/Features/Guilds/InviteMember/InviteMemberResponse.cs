namespace Harmonie.Application.Features.Guilds.InviteMember;

public sealed record InviteMemberResponse(
    string GuildId,
    string UserId,
    string Role,
    DateTime JoinedAtUtc);
