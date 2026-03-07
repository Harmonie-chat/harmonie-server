using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

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
    DateTime JoinedAtUtc);

public sealed record VoiceParticipantLeftNotification(
    GuildId GuildId,
    GuildChannelId ChannelId,
    UserId UserId,
    string ParticipantName,
    DateTime LeftAtUtc);
