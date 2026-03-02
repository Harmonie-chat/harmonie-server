using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public sealed record GuildAccessContext(
    Guild Guild,
    GuildRole? CallerRole);

public interface IGuildRepository
{
    Task<Guild?> GetByIdAsync(GuildId guildId, CancellationToken cancellationToken = default);

    Task<GuildAccessContext?> GetWithCallerRoleAsync(
        GuildId guildId,
        UserId callerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Guild guild, CancellationToken cancellationToken = default);

    Task UpdateOwnerAsync(GuildId guildId, UserId newOwnerId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(GuildId guildId, CancellationToken cancellationToken = default);
}
