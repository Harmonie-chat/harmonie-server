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

    public async Task NotifyChannelUpdatedAsync(
        ChannelUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ChannelUpdatedEvent(
            GuildId: notification.GuildId.Value,
            ChannelId: notification.ChannelId.Value,
            Name: notification.Name,
            Position: notification.Position);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("ChannelUpdated", payload, cancellationToken);
    }

    public async Task NotifyChannelDeletedAsync(
        ChannelDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ChannelDeletedEvent(
            GuildId: notification.GuildId.Value,
            ChannelId: notification.ChannelId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("ChannelDeleted", payload, cancellationToken);
    }

    public async Task NotifyChannelsReorderedAsync(
        ChannelsReorderedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new ChannelsReorderedEvent(
            GuildId: notification.GuildId.Value,
            Channels: notification.Channels
                .Select(c => new ChannelPositionItemEvent(c.ChannelId.Value, c.Position))
                .ToArray());

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("ChannelsReordered", payload, cancellationToken);
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

public sealed record ChannelUpdatedEvent(
    Guid GuildId,
    Guid ChannelId,
    string Name,
    int Position);

public sealed record ChannelDeletedEvent(
    Guid GuildId,
    Guid ChannelId);

public sealed record ChannelsReorderedEvent(
    Guid GuildId,
    ChannelPositionItemEvent[] Channels);

public sealed record ChannelPositionItemEvent(
    Guid ChannelId,
    int Position);
