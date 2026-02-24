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
}

public sealed record UserGuildMembership(
    Guild Guild,
    GuildRole Role,
    DateTime JoinedAtUtc);
