using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public sealed record GetGuildMembersResponse(
    Guid GuildId,
    IReadOnlyList<GetGuildMembersItemResponse> Members);

public sealed record GetGuildMembersItemResponse(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar,
    string? Bio,
    bool IsActive,
    string Role,
    DateTime JoinedAtUtc);
