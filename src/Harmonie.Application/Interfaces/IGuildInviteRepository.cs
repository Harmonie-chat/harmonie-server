using Harmonie.Domain.Entities;

namespace Harmonie.Application.Interfaces;

public interface IGuildInviteRepository
{
    Task AddAsync(GuildInvite invite, CancellationToken cancellationToken = default);
}
