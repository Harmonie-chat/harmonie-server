using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Users;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Users;

public sealed class SignalRUserProfileNotifier : IUserProfileNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRUserProfileNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyProfileUpdatedAsync(
        UserProfileUpdatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new UserProfileUpdatedEvent(
            UserId: notification.UserId.Value,
            DisplayName: notification.DisplayName,
            AvatarFileId: notification.AvatarFileId?.Value);

        var broadcastTasks = notification.GuildIds
            .Select(guildId =>
                _hubContext.Clients
                    .Group(RealtimeHub.GetGuildGroupName(guildId))
                    .UserProfileUpdated(payload, cancellationToken))
            .Concat(notification.ConversationIds
                .Select(conversationId =>
                    _hubContext.Clients
                        .Group(RealtimeHub.GetConversationGroupName(conversationId))
                        .UserProfileUpdated(payload, cancellationToken)));

        await Task.WhenAll(broadcastTasks);
    }
}

public sealed record UserProfileUpdatedEvent(
    Guid UserId,
    string? DisplayName,
    Guid? AvatarFileId);
