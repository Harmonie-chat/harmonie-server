using Harmonie.Domain.Entities;

namespace Harmonie.Application.Interfaces;

public interface IDirectMessageRepository
{
    Task AddAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default);
}
