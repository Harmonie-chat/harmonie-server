using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Guilds;

public interface IGuildMemberRepository
{
    Task<bool> IsMemberAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<GuildMemberUserRole?> GetUserWithRoleAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(
        GuildMember member,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserGuildMembership>> GetUserGuildMembershipsAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildMemberUser>> GetGuildMembersAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<int> UpdateRoleAsync(
        GuildId guildId,
        UserId userId,
        GuildRole newRole,
        CancellationToken cancellationToken = default);
}

public sealed record UserGuildMembership(
    Guild Guild,
    GuildRole Role,
    DateTime JoinedAtUtc);

public sealed record GuildMemberUserRole(
    GuildRole Role,
    string Username,
    string? DisplayName);

public sealed record GuildMemberUser(
    UserId UserId,
    Username Username,
    string? DisplayName,
    UploadedFileId? AvatarFileId,
    string? Bio,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    bool IsActive,
    GuildRole Role,
    DateTime JoinedAtUtc);
