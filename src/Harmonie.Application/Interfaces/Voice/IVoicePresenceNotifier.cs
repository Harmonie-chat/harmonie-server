using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Voice;

public interface IVoicePresenceNotifier
{
    Task NotifyParticipantJoinedAsync(
        VoiceParticipantJoinedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyParticipantLeftAsync(
        VoiceParticipantLeftNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyScreenShareStartedAsync(
        VoiceScreenShareNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyScreenShareStoppedAsync(
        VoiceScreenShareNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record VoiceParticipantJoinedNotification(
    GuildId GuildId,
    string GuildName,
    GuildChannelId ChannelId,
    string ChannelName,
    UserId UserId,
    string? Username,
    string? DisplayName,
    UploadedFileId? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    DateTime JoinedAtUtc);

public sealed record VoiceParticipantLeftNotification(
    GuildId GuildId,
    string GuildName,
    GuildChannelId ChannelId,
    string ChannelName,
    UserId UserId,
    string? Username,
    DateTime LeftAtUtc);

public sealed record VoiceScreenShareNotification(
    GuildId GuildId,
    string GuildName,
    GuildChannelId ChannelId,
    string ChannelName,
    UserId UserId,
    string? Username,
    DateTime TimestampUtc);
