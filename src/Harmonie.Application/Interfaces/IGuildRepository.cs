using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IGuildRepository
{
    Task<Guild?> GetByIdAsync(GuildId guildId, CancellationToken cancellationToken = default);

    Task AddAsync(Guild guild, CancellationToken cancellationToken = default);

    Task UpdateOwnerAsync(GuildId guildId, UserId newOwnerId, CancellationToken cancellationToken = default);
}
