using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IUserPresenceNotifier
{
    Task NotifyStatusChangedAsync(
        UserPresenceChangedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record UserPresenceChangedNotification(
    UserId UserId,
    string Status,
    IReadOnlyList<GuildId> GuildIds);
