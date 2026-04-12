using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Guilds;

public sealed class SignalRGuildNotifier : IGuildNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;
    private readonly IConnectionTracker _connectionTracker;

    public SignalRGuildNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext, IConnectionTracker connectionTracker)
    {
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
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
            .GuildDeleted(payload, cancellationToken);
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
            .GuildOwnershipTransferred(payload, cancellationToken);
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
            .ChannelCreated(payload, cancellationToken);
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
            .ChannelUpdated(payload, cancellationToken);
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
            .ChannelDeleted(payload, cancellationToken);
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
            .ChannelsReordered(payload, cancellationToken);
    }

    public async Task NotifyMemberJoinedAsync(
        MemberJoinedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MemberJoinedEvent(
            GuildId: notification.GuildId.Value,
            UserId: notification.UserId.Value,
            DisplayName: notification.DisplayName,
            AvatarFileId: notification.AvatarFileId?.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberJoined(payload, cancellationToken);
    }

    public async Task NotifyMemberLeftAsync(
        MemberLeftNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MemberLeftEvent(
            GuildId: notification.GuildId.Value,
            UserId: notification.UserId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberLeft(payload, cancellationToken);
    }

    public async Task NotifyMemberBannedAsync(
        MemberBannedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var memberBannedPayload = new MemberBannedEvent(
            GuildId: notification.GuildId.Value,
            UserId: notification.BannedUserId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberBanned(memberBannedPayload, cancellationToken);

        var connectionIds = _connectionTracker.GetConnectionIds(notification.BannedUserId);
        if (connectionIds.Count > 0)
        {
            var youWereBannedPayload = new YouWereBannedEvent(
                GuildId: notification.GuildId.Value);

            await _hubContext.Clients
                .Clients(connectionIds)
                .YouWereBanned(youWereBannedPayload, cancellationToken);
        }
    }

    public async Task NotifyMemberRemovedAsync(
        MemberRemovedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var memberRemovedPayload = new MemberRemovedEvent(
            GuildId: notification.GuildId.Value,
            UserId: notification.RemovedUserId.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberRemoved(memberRemovedPayload, cancellationToken);

        var connectionIds = _connectionTracker.GetConnectionIds(notification.RemovedUserId);
        if (connectionIds.Count > 0)
        {
            var youWereKickedPayload = new YouWereKickedEvent(
                GuildId: notification.GuildId.Value);

            await _hubContext.Clients
                .Clients(connectionIds)
                .YouWereKicked(youWereKickedPayload, cancellationToken);
        }
    }

    public async Task NotifyMemberRoleUpdatedAsync(
        MemberRoleUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new MemberRoleUpdatedEvent(
            GuildId: notification.GuildId.Value,
            UserId: notification.UserId.Value,
            NewRole: notification.NewRole.ToString());

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberRoleUpdated(payload, cancellationToken);
    }

    public async Task NotifyGuildUpdatedAsync(
        GuildUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new GuildUpdatedEvent(
            GuildId: notification.GuildId.Value,
            Name: notification.Name,
            IconFileId: notification.IconFileId?.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .GuildUpdated(payload, cancellationToken);
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

public sealed record MemberJoinedEvent(
    Guid GuildId,
    Guid UserId,
    string? DisplayName,
    Guid? AvatarFileId);

public sealed record MemberLeftEvent(
    Guid GuildId,
    Guid UserId);

public sealed record MemberBannedEvent(
    Guid GuildId,
    Guid UserId);

public sealed record YouWereBannedEvent(
    Guid GuildId);

public sealed record MemberRemovedEvent(
    Guid GuildId,
    Guid UserId);

public sealed record YouWereKickedEvent(
    Guid GuildId);

public sealed record MemberRoleUpdatedEvent(
    Guid GuildId,
    Guid UserId,
    string NewRole);

public sealed record GuildUpdatedEvent(
    Guid GuildId,
    string Name,
    Guid? IconFileId);
