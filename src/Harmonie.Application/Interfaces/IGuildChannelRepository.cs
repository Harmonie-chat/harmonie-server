using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IGuildChannelRepository
{
    Task AddAsync(
        GuildChannel channel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildChannel>> GetByGuildIdAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default);
}
