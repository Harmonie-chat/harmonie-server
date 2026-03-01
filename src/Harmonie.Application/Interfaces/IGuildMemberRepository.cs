using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IGuildMemberRepository
{
    Task<bool> IsMemberAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<GuildRole?> GetRoleAsync(
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

public sealed record GuildMemberUser(
    UserId UserId,
    Username Username,
    string? DisplayName,
    string? AvatarUrl,
    bool IsActive,
    GuildRole Role,
    DateTime JoinedAtUtc);
