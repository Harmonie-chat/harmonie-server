using Harmonie.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

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
            UserId: notification.UserId.ToString(),
            Status: notification.Status);

        var broadcastTasks = notification.GuildIds.Select(guildId =>
            _hubContext.Clients
                .Group(RealtimeHub.GetGuildGroupName(guildId))
                .SendAsync("UserPresenceChanged", payload, cancellationToken));

        await Task.WhenAll(broadcastTasks);
    }
}

public sealed record UserPresenceChangedEvent(
    string UserId,
    string Status);
