namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public sealed record GetGuildMembersResponse(
    string GuildId,
    IReadOnlyList<GetGuildMembersItemResponse> Members);

public sealed record GetGuildMembersItemResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? AvatarFileId,
    bool IsActive,
    string Role,
    DateTime JoinedAtUtc);
