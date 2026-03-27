namespace Harmonie.Application.Features.Guilds.InviteMember;

public sealed record InviteMemberResponse(
    Guid GuildId,
    Guid UserId,
    string Role,
    DateTime JoinedAtUtc);
