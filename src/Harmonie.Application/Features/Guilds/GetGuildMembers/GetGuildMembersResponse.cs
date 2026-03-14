using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public sealed record GetGuildMembersResponse(
    string GuildId,
    IReadOnlyList<GetGuildMembersItemResponse> Members);

public sealed record GetGuildMembersItemResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string? Bio,
    bool IsActive,
    string Role,
    DateTime JoinedAtUtc);
