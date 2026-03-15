using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IConnectionTracker
{
    Task HandleConnectedAsync(UserId userId, string connectionId, CancellationToken cancellationToken = default);
    Task HandleDisconnectedAsync(UserId userId, string connectionId, CancellationToken cancellationToken = default);
    bool IsOnline(UserId userId);
}
