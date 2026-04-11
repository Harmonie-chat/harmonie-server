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
}

public sealed record VoiceParticipantJoinedNotification(
    GuildId GuildId,
    GuildChannelId ChannelId,
    UserId UserId,
    string ParticipantName,
    string? DisplayName,
    UploadedFileId? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    DateTime JoinedAtUtc);

public sealed record VoiceParticipantLeftNotification(
    GuildId GuildId,
    GuildChannelId ChannelId,
    UserId UserId,
    string ParticipantName,
    DateTime LeftAtUtc);
