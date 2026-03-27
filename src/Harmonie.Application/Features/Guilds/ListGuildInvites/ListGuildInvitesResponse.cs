namespace Harmonie.Application.Features.Guilds.ListGuildInvites;

public sealed record ListGuildInvitesResponse(
    Guid GuildId,
    IReadOnlyList<ListGuildInvitesItemResponse> Invites);

public sealed record ListGuildInvitesItemResponse(
    string Code,
    Guid CreatorId,
    int UsesCount,
    int? MaxUses,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    bool IsExpired);
