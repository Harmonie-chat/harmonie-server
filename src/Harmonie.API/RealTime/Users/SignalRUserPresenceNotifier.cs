using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Users;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Users;

public sealed class SignalRUserPresenceNotifier : IUserPresenceNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRUserPresenceNotifier(IHubContext<RealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyStatusChangedAsync(
        UserPresenceChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new UserPresenceChangedEvent(
            UserId: notification.UserId.Value,
            Status: notification.Status);

        var broadcastTasks = notification.GuildIds.Select(guildId =>
            _hubContext.Clients
                .Group(RealtimeHub.GetGuildGroupName(guildId))
                .SendAsync("UserPresenceChanged", payload, cancellationToken));

        await Task.WhenAll(broadcastTasks);
    }
}

public sealed record UserPresenceChangedEvent(
    Guid UserId,
    string Status);
