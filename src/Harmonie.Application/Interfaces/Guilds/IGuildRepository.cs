using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Guilds;

public sealed record GuildAccessContext(
    Guild Guild,
    GuildRole? CallerRole,
    string? CallerUsername = null,
    string? CallerDisplayName = null);

public interface IGuildRepository
{
    Task<Guild?> GetByIdAsync(GuildId guildId, CancellationToken cancellationToken = default);

    Task<GuildAccessContext?> GetWithCallerRoleAsync(
        GuildId guildId,
        UserId callerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Guild guild, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guild guild, CancellationToken cancellationToken = default);

    Task DeleteAsync(GuildId guildId, CancellationToken cancellationToken = default);

    Task UpdateOwnerAsync(GuildId guildId, UserId newOwnerId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(GuildId guildId, CancellationToken cancellationToken = default);
}
