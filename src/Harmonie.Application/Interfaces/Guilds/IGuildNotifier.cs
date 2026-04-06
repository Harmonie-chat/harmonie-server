using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
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

    Task NotifyChannelCreatedAsync(
        ChannelCreatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record GuildDeletedNotification(
    GuildId GuildId);

public sealed record GuildOwnershipTransferredNotification(
    GuildId GuildId,
    UserId NewOwnerUserId);

public sealed record ChannelCreatedNotification(
    GuildId GuildId,
    GuildChannelId ChannelId,
    string Name,
    GuildChannelType Type,
    bool IsDefault,
    int Position);
