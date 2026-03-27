namespace Harmonie.Application.Features.Guilds.AcceptInvite;

public sealed record AcceptInviteResponse(
    Guid GuildId,
    Guid UserId,
    string Role,
    DateTime JoinedAtUtc);
