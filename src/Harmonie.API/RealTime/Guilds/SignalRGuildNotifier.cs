using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Guilds;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Guilds;

public sealed class SignalRGuildNotifier : IGuildNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRGuildNotifier(IHubContext<RealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyGuildDeletedAsync(
        GuildDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new GuildDeletedEvent(
            GuildId: notification.GuildId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("GuildDeleted", payload, cancellationToken);
    }

    public async Task NotifyGuildOwnershipTransferredAsync(
        GuildOwnershipTransferredNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new GuildOwnershipTransferredEvent(
            GuildId: notification.GuildId.Value,
            NewOwnerUserId: notification.NewOwnerUserId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("GuildOwnershipTransferred", payload, cancellationToken);
    }

    public async Task NotifyChannelCreatedAsync(
        ChannelCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ChannelCreatedEvent(
            GuildId: notification.GuildId.Value,
            ChannelId: notification.ChannelId.Value,
            Name: notification.Name,
            Type: notification.Type.ToString(),
            IsDefault: notification.IsDefault,
            Position: notification.Position);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("ChannelCreated", payload, cancellationToken);
    }
}

public sealed record GuildDeletedEvent(
    Guid GuildId);

public sealed record GuildOwnershipTransferredEvent(
    Guid GuildId,
    Guid NewOwnerUserId);

public sealed record ChannelCreatedEvent(
    Guid GuildId,
    Guid ChannelId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);
