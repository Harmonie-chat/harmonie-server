namespace Harmonie.Application.Features.Guilds.ListGuildInvites;

public sealed record ListGuildInvitesResponse(
    string GuildId,
    IReadOnlyList<ListGuildInvitesItemResponse> Invites);

public sealed record ListGuildInvitesItemResponse(
    string Code,
    string CreatorId,
    int UsesCount,
    int? MaxUses,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc,
    bool IsExpired);
