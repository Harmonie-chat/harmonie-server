using System.Collections.Concurrent;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.API.RealTime.Voice.Conversations;

public sealed class InMemoryConversationVoiceParticipantCache : IConversationVoiceParticipantCache
{
    private static readonly TimeSpan ParticipantTtl = TimeSpan.FromHours(1);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CachedEntry>> _conversations = new();
    private readonly ConcurrentDictionary<(Guid ConversationId, Guid UserId), ConcurrentDictionary<string, byte>> _screenShareTrackSids = new();

    public InMemoryConversationVoiceParticipantCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task AddOrUpdateAsync(ConversationId conversationId, CachedVoiceParticipant participant, CancellationToken cancellationToken = default)
    {
        var conversation = _conversations.GetOrAdd(conversationId.Value, _ => new ConcurrentDictionary<Guid, CachedEntry>());
        conversation[participant.UserId.Value] = new CachedEntry(participant, _timeProvider.GetUtcNow().UtcDateTime + ParticipantTtl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(ConversationId conversationId, UserId userId, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId.Value, out var conversation))
            conversation.TryRemove(userId.Value, out _);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CachedVoiceParticipant>> GetAsync(ConversationId conversationId, CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId.Value, out var conversation))
            return Task.FromResult<IReadOnlyList<CachedVoiceParticipant>>(Array.Empty<CachedVoiceParticipant>());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = conversation.Values
            .Where(e => e.ExpiresAt > now)
            .Select(e =>
            {
                var isSharingScreen = _screenShareTrackSids.TryGetValue(
                    (conversationId.Value, e.Participant.UserId.Value), out var sids) && !sids.IsEmpty;

                return e.Participant with { IsSharingScreen = isSharingScreen };
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<CachedVoiceParticipant>>(result);
    }

    public Task<ScreenShareTrackAddResult> TryAddScreenShareTrackAsync(
        ConversationId conversationId,
        UserId userId,
        string trackSid,
        CancellationToken cancellationToken = default)
    {
        var key = (conversationId.Value, userId.Value);
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
        ConversationId conversationId,
        UserId userId,
        string trackSid,
        CancellationToken cancellationToken = default)
    {
        var key = (conversationId.Value, userId.Value);
        if (_screenShareTrackSids.TryGetValue(key, out var sids))
        {
            sids.TryRemove(trackSid, out _);
        }

        var wasPresentAndNowEmpty = sids is not null && sids.IsEmpty;

        if (wasPresentAndNowEmpty)
            _screenShareTrackSids.TryRemove(key, out _);

        return Task.FromResult(new ScreenShareTrackRemoveResult(wasPresentAndNowEmpty));
    }

    public Task<bool> ClearScreenShareTracksAsync(
        ConversationId conversationId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = (conversationId.Value, userId.Value);
        if (_screenShareTrackSids.TryRemove(key, out var sids))
            return Task.FromResult(!sids.IsEmpty);

        return Task.FromResult(false);
    }

    private sealed record CachedEntry(CachedVoiceParticipant Participant, DateTime ExpiresAt);
}
