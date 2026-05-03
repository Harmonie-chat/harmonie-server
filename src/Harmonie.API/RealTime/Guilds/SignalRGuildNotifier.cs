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
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName);

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
            GuildName: notification.GuildName,
            NewOwnerUserId: notification.NewOwnerUserId.Value,
            NewOwnerUsername: notification.NewOwnerUsername,
            NewOwnerDisplayName: notification.NewOwnerDisplayName);

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
            GuildName: notification.GuildName,
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
            GuildName: notification.GuildName,
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
            GuildName: notification.GuildName,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName);

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
            GuildName: notification.GuildName,
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
            GuildName: notification.GuildName,
            UserId: notification.UserId.Value,
            Username: notification.Username,
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
            GuildName: notification.GuildName,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            DisplayName: notification.DisplayName);

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
            GuildName: notification.GuildName,
            UserId: notification.BannedUserId.Value,
            Username: notification.Username,
            DisplayName: notification.DisplayName);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberBanned(memberBannedPayload, cancellationToken);

        var connectionIds = _connectionTracker.GetConnectionIds(notification.BannedUserId);
        if (connectionIds.Count > 0)
        {
            var youWereBannedPayload = new YouWereBannedEvent(
                GuildId: notification.GuildId.Value,
                GuildName: notification.GuildName);

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
            GuildName: notification.GuildName,
            UserId: notification.RemovedUserId.Value,
            Username: notification.Username,
            DisplayName: notification.DisplayName);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .MemberRemoved(memberRemovedPayload, cancellationToken);

        var connectionIds = _connectionTracker.GetConnectionIds(notification.RemovedUserId);
        if (connectionIds.Count > 0)
        {
            var youWereKickedPayload = new YouWereKickedEvent(
                GuildId: notification.GuildId.Value,
                GuildName: notification.GuildName);

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
            GuildName: notification.GuildName,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            DisplayName: notification.DisplayName,
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
            GuildName: notification.Name,
            IconFileId: notification.IconFileId?.Value);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .GuildUpdated(payload, cancellationToken);
    }
}

public sealed record GuildDeletedEvent(
    Guid GuildId,
    string GuildName);

public sealed record GuildOwnershipTransferredEvent(
    Guid GuildId,
    string GuildName,
    Guid NewOwnerUserId,
    string NewOwnerUsername,
    string? NewOwnerDisplayName);

public sealed record ChannelCreatedEvent(
    Guid GuildId,
    string GuildName,
    Guid ChannelId,
    string Name,
    string Type,
    bool IsDefault,
    int Position);

public sealed record ChannelUpdatedEvent(
    Guid GuildId,
    string GuildName,
    Guid ChannelId,
    string Name,
    int Position);

public sealed record ChannelDeletedEvent(
    Guid GuildId,
    string GuildName,
    Guid ChannelId,
    string ChannelName);

public sealed record ChannelsReorderedEvent(
    Guid GuildId,
    string GuildName,
    ChannelPositionItemEvent[] Channels);

public sealed record ChannelPositionItemEvent(
    Guid ChannelId,
    int Position);

public sealed record MemberJoinedEvent(
    Guid GuildId,
    string GuildName,
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId);

public sealed record MemberLeftEvent(
    Guid GuildId,
    string GuildName,
    Guid UserId,
    string Username,
    string? DisplayName);

public sealed record MemberBannedEvent(
    Guid GuildId,
    string GuildName,
    Guid UserId,
    string Username,
    string? DisplayName);

public sealed record YouWereBannedEvent(
    Guid GuildId,
    string GuildName);

public sealed record MemberRemovedEvent(
    Guid GuildId,
    string GuildName,
    Guid UserId,
    string Username,
    string? DisplayName);

public sealed record YouWereKickedEvent(
    Guid GuildId,
    string GuildName);

public sealed record MemberRoleUpdatedEvent(
    Guid GuildId,
    string GuildName,
    Guid UserId,
    string Username,
    string? DisplayName,
    string NewRole);

public sealed record GuildUpdatedEvent(
    Guid GuildId,
    string GuildName,
    Guid? IconFileId);
