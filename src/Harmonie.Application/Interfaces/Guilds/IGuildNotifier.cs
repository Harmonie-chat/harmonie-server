using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Guilds;

public interface IGuildNotifier
{
    Task NotifyGuildDeletedAsync(
        GuildDeletedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyGuildOwnershipTransferredAsync(
        GuildOwnershipTransferredNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record GuildDeletedNotification(
    GuildId GuildId);

public sealed record GuildOwnershipTransferredNotification(
    GuildId GuildId,
    UserId NewOwnerUserId);
