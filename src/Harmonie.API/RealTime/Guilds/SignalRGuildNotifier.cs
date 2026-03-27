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
}

public sealed record GuildDeletedEvent(
    Guid GuildId);
