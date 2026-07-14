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
    [Fact]
    public async Task GuildCache_ShouldExpireParticipantUsingInjectedTimeProvider()
    {
        var timeProvider = TestTime.CreateProvider();
        var cache = new InMemoryVoiceParticipantCache(timeProvider);
        var channelId = GuildChannelId.New();
        var participant = CreateParticipant();

        await cache.AddOrUpdateAsync(channelId, participant, TestContext.Current.CancellationToken);
        (await cache.GetAsync(channelId, TestContext.Current.CancellationToken)).Should().ContainSingle();

        timeProvider.Advance(TimeSpan.FromHours(1));

        (await cache.GetAsync(channelId, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationCache_ShouldExpireParticipantUsingInjectedTimeProvider()
    {
        var timeProvider = TestTime.CreateProvider();
        var cache = new InMemoryConversationVoiceParticipantCache(timeProvider);
        var conversationId = ConversationId.New();
        var participant = CreateParticipant();

        await cache.AddOrUpdateAsync(conversationId, participant, TestContext.Current.CancellationToken);
        (await cache.GetAsync(conversationId, TestContext.Current.CancellationToken)).Should().ContainSingle();

        timeProvider.Advance(TimeSpan.FromHours(1));

        (await cache.GetAsync(conversationId, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static CachedVoiceParticipant CreateParticipant()
        => new(UserId.New(), "clock-user", null, null, null, null, null);

}
