using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.ListBans;

public sealed record ListBansResponse(
    string GuildId,
    IReadOnlyList<ListBansItemResponse> Bans);

public sealed record ListBansItemResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string? Reason,
    string BannedBy,
    DateTime CreatedAtUtc);
