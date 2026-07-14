using System.Collections.Concurrent;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.API.RealTime.Voice;

public sealed class InMemoryVoiceParticipantCache : IVoiceParticipantCache
{
    private static readonly TimeSpan ParticipantTtl = TimeSpan.FromHours(1);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CachedEntry>> _channels = new();
    private readonly ConcurrentDictionary<(Guid ChannelId, Guid UserId), ConcurrentDictionary<string, byte>> _screenShareTrackSids = new();

    public InMemoryVoiceParticipantCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task AddOrUpdateAsync(GuildChannelId channelId, CachedVoiceParticipant participant, CancellationToken cancellationToken = default)
    {
        var channel = _channels.GetOrAdd(channelId.Value, _ => new ConcurrentDictionary<Guid, CachedEntry>());
        channel[participant.UserId.Value] = new CachedEntry(participant, _timeProvider.GetUtcNow().UtcDateTime + ParticipantTtl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(GuildChannelId channelId, UserId userId, CancellationToken cancellationToken = default)
    {
        if (_channels.TryGetValue(channelId.Value, out var channel))
            channel.TryRemove(userId.Value, out _);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CachedVoiceParticipant>> GetAsync(GuildChannelId channelId, CancellationToken cancellationToken = default)
    {
        if (!_channels.TryGetValue(channelId.Value, out var channel))
            return Task.FromResult<IReadOnlyList<CachedVoiceParticipant>>(Array.Empty<CachedVoiceParticipant>());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = channel.Values
            .Where(e => e.ExpiresAt > now)
            .Select(e =>
            {
                var isSharingScreen = _screenShareTrackSids.TryGetValue(
                    (channelId.Value, e.Participant.UserId.Value), out var sids) && !sids.IsEmpty;

                return e.Participant with { IsSharingScreen = isSharingScreen };
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<CachedVoiceParticipant>>(result);
    }

    public Task<ScreenShareTrackAddResult> TryAddScreenShareTrackAsync(
        GuildChannelId channelId,
        UserId userId,
        string trackSid,
        CancellationToken cancellationToken = default)
    {
        var key = (channelId.Value, userId.Value);
        var sids = _screenShareTrackSids.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>());
        bool isFirst;
        lock (sids)
        {
            var wasEmpty = sids.IsEmpty;
            sids[trackSid] = 0;
            isFirst = wasEmpty;
        }

        return Task.FromResult(new ScreenShareTrackAddResult(isFirst));
    }

    public Task<ScreenShareTrackRemoveResult> TryRemoveScreenShareTrackAsync(
        GuildChannelId channelId,
        UserId userId,
        string trackSid,
        CancellationToken cancellationToken = default)
    {
        var key = (channelId.Value, userId.Value);
        if (_screenShareTrackSids.TryGetValue(key, out var sids))
        {
            sids.TryRemove(trackSid, out _);
        }

        var wasPresentAndNowEmpty = sids is not null && sids.IsEmpty;

        // Clean up empty sets to prevent memory leaks
        if (wasPresentAndNowEmpty)
            _screenShareTrackSids.TryRemove(key, out _);

        return Task.FromResult(new ScreenShareTrackRemoveResult(wasPresentAndNowEmpty));
    }

    public Task<bool> ClearScreenShareTracksAsync(
        GuildChannelId channelId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = (channelId.Value, userId.Value);
        if (_screenShareTrackSids.TryRemove(key, out var sids))
            return Task.FromResult(!sids.IsEmpty);

        return Task.FromResult(false);
    }

    private sealed record CachedEntry(CachedVoiceParticipant Participant, DateTime ExpiresAt);
}
