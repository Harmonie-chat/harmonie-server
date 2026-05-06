using Harmonie.API.RealTime.Common;
using Harmonie.Application.Interfaces.Voice;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Voice;

public sealed class SignalRVoicePresenceNotifier : IVoicePresenceNotifier
{
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;

    public SignalRVoicePresenceNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext)
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
            GuildName: notification.GuildName,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName,
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
            .VoiceParticipantJoined(payload, cancellationToken);
    }

    public async Task NotifyParticipantLeftAsync(
        VoiceParticipantLeftNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new VoiceParticipantLeftEvent(
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            LeftAtUtc: notification.LeftAtUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .VoiceParticipantLeft(payload, cancellationToken);
    }

    public async Task NotifyScreenShareStartedAsync(
        VoiceScreenShareNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new VoiceScreenShareEvent(
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            TimestampUtc: notification.TimestampUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .VoiceScreenShareStarted(payload, cancellationToken);
    }

    public async Task NotifyScreenShareStoppedAsync(
        VoiceScreenShareNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var payload = new VoiceScreenShareEvent(
            GuildId: notification.GuildId.Value,
            GuildName: notification.GuildName,
            ChannelId: notification.ChannelId.Value,
            ChannelName: notification.ChannelName,
            UserId: notification.UserId.Value,
            Username: notification.Username,
            TimestampUtc: notification.TimestampUtc);

        await _hubContext.Clients
            .Group(RealtimeHub.GetGuildGroupName(notification.GuildId))
            .VoiceScreenShareStopped(payload, cancellationToken);
    }
}

public sealed record VoiceParticipantJoinedEvent(
    Guid GuildId,
    string GuildName,
    Guid ChannelId,
    string ChannelName,
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
    string GuildName,
    Guid ChannelId,
    string ChannelName,
    Guid UserId,
    string? Username,
    DateTime LeftAtUtc);

public sealed record VoiceScreenShareEvent(
    Guid GuildId,
    string GuildName,
    Guid ChannelId,
    string ChannelName,
    Guid UserId,
    string? Username,
    DateTime TimestampUtc);
