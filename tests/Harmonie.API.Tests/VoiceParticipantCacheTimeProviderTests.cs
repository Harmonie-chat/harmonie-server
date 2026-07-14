using FluentAssertions;
using Harmonie.API.RealTime.Voice;
using Harmonie.API.RealTime.Voice.Conversations;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.API.Tests;

public sealed class VoiceParticipantCacheTimeProviderTests
{
    private static readonly DateTimeOffset InitialUtcNow = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GuildCache_ShouldExpireParticipantUsingInjectedTimeProvider()
    {
        var timeProvider = new MutableTimeProvider(InitialUtcNow);
        var cache = new InMemoryVoiceParticipantCache(timeProvider);
        var channelId = GuildChannelId.New();
        var participant = CreateParticipant();

        await cache.AddOrUpdateAsync(channelId, participant, TestContext.Current.CancellationToken);
        (await cache.GetAsync(channelId, TestContext.Current.CancellationToken)).Should().ContainSingle();

        timeProvider.SetUtcNow(InitialUtcNow.AddHours(1));

        (await cache.GetAsync(channelId, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationCache_ShouldExpireParticipantUsingInjectedTimeProvider()
    {
        var timeProvider = new MutableTimeProvider(InitialUtcNow);
        var cache = new InMemoryConversationVoiceParticipantCache(timeProvider);
        var conversationId = ConversationId.New();
        var participant = CreateParticipant();

        await cache.AddOrUpdateAsync(conversationId, participant, TestContext.Current.CancellationToken);
        (await cache.GetAsync(conversationId, TestContext.Current.CancellationToken)).Should().ContainSingle();

        timeProvider.SetUtcNow(InitialUtcNow.AddHours(1));

        (await cache.GetAsync(conversationId, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static CachedVoiceParticipant CreateParticipant()
        => new(UserId.New(), "clock-user", null, null, null, null, null);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value)
        {
            _utcNow = value;
        }
    }
}
