using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class JoinVoiceChannelTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JoinVoiceChannelTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenGuildMemberJoinsVoiceChannel_ShouldReturn200()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Voice Join Guild");
        var voiceChannelId = await GetDefaultVoiceChannelIdAsync(owner.AccessToken, guildId);

        var joinResponse = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/channels/{voiceChannelId}/voice/join",
            owner.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await joinResponse.Content.ReadFromJsonAsync<JoinVoiceChannelResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Token.Should().NotBeNullOrWhiteSpace();
        payload.Url.Should().Be("ws://localhost:7880");
        payload.RoomName.Should().Be($"channel:{voiceChannelId}");
        payload.CurrentParticipants.Should().NotBeNull();
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenChannelIsText_ShouldReturn409()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "text-only-channel");

        var joinResponse = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/channels/{channelId}/voice/join",
            owner.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotVoice);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenUserIsNotGuildMember_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Forbidden Voice Join Guild");
        var voiceChannelId = await GetDefaultVoiceChannelIdAsync(owner.AccessToken, guildId);

        var joinResponse = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/channels/{voiceChannelId}/voice/join",
            outsider.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenChannelDoesNotExist_ShouldReturn404()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var joinResponse = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/channels/{Guid.NewGuid()}/voice/join",
            user.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.PostAsync(
            $"/api/channels/{Guid.NewGuid()}/voice/join",
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> GetDefaultVoiceChannelIdAsync(
        string accessToken,
        Guid guildId)
    {
        var response = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();

        return payload!.Channels.First(channel => channel.Type == "Voice").ChannelId;
    }
}
