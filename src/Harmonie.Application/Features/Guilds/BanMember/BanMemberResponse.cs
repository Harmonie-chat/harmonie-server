namespace Harmonie.Application.Features.Guilds.BanMember;

public sealed record BanMemberResponse(
    Guid GuildId,
    Guid UserId,
    string? Reason,
    Guid BannedBy,
    DateTime CreatedAtUtc);
