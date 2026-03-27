using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.ListBans;

public sealed record ListBansResponse(
    Guid GuildId,
    IReadOnlyList<ListBansItemResponse> Bans);

public sealed record ListBansItemResponse(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string? Reason,
    Guid BannedBy,
    DateTime CreatedAtUtc);
