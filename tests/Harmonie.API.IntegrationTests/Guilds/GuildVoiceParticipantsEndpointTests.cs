using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildVoiceParticipantsEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GuildVoiceParticipantsEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGuildVoiceParticipants_WhenRequesterIsMember_ShouldReturnParticipantsGroupedByVoiceChannel()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Voice Snapshot Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var voiceChannels = channelsPayload!.Channels
            .Where(channel => channel.Type == "Voice")
            .ToArray();
        voiceChannels.Should().HaveCount(1);

        var fakeRoomService = new FakeLiveKitRoomService();
        fakeRoomService.ResultsByGuildId[createGuildPayload.GuildId] =
        [
            new GuildVoiceChannelParticipants(
                GuildChannelId.From(Guid.Parse(voiceChannels[0].ChannelId)),
                [
                    new VoiceChannelParticipant(UserId.From(Guid.Parse(owner.UserId)), owner.Username)
                ])
        ];

        using var clientFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILiveKitRoomService>();
                services.AddSingleton<ILiveKitRoomService>(fakeRoomService);
            });
        });

        using var client = clientFactory.CreateClient();
        var response = await client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/voice/participants",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildVoiceParticipantsResponse>();
        payload.Should().NotBeNull();
        payload!.Channels.Should().HaveCount(1);
        payload.Channels.Should().Contain(channel =>
            channel.ChannelId == voiceChannels[0].ChannelId
            && channel.Participants.Count == 1
            && channel.Participants[0].UserId == owner.UserId
            && channel.Participants[0].Username == owner.Username);
    }

    [Fact]
    public async Task GetGuildVoiceParticipants_WhenRequesterIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Voice Snapshot Forbidden Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/voice/participants",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    private sealed class FakeLiveKitRoomService : ILiveKitRoomService
    {
        public Dictionary<string, IReadOnlyList<GuildVoiceChannelParticipants>> ResultsByGuildId { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<GuildVoiceChannelParticipants>> GetGuildVoiceParticipantsAsync(
            GuildId guildId,
            CancellationToken ct)
            => Task.FromResult(
                ResultsByGuildId.TryGetValue(guildId.ToString(), out var results)
                    ? results
                    : (IReadOnlyList<GuildVoiceChannelParticipants>)[]);
    }
}
