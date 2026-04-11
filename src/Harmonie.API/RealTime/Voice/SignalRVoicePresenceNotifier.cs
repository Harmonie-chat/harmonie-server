using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Voice;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Voice;

public sealed class SignalRVoicePresenceNotifier : IVoicePresenceNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRVoicePresenceNotifier(IHubContext<RealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyParticipantJoinedAsync(
        VoiceParticipantJoinedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new VoiceParticipantJoinedEvent(
            GuildId: notification.GuildId.Value,
            ChannelId: notification.ChannelId.Value,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            DisplayName: notification.DisplayName,
            AvatarFileId: notification.AvatarFileId?.Value,
            AvatarColor: notification.AvatarColor,
            AvatarIcon: notification.AvatarIcon,
            AvatarBg: notification.AvatarBg,
            JoinedAtUtc: notification.JoinedAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("VoiceParticipantJoined", payload, cancellationToken);
    }

    public async Task NotifyParticipantLeftAsync(
        VoiceParticipantLeftNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new VoiceParticipantLeftEvent(
            GuildId: notification.GuildId.Value,
            ChannelId: notification.ChannelId.Value,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            LeftAtUtc: notification.LeftAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .SendAsync("VoiceParticipantLeft", payload, cancellationToken);
    }
}

public sealed record VoiceParticipantJoinedEvent(
    Guid GuildId,
    Guid ChannelId,
    Guid UserId,
    string? Username,
    string? DisplayName,
    Guid? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    DateTime JoinedAtUtc);

public sealed record VoiceParticipantLeftEvent(
    Guid GuildId,
    Guid ChannelId,
    Guid UserId,
    string? Username,
    DateTime LeftAtUtc);
