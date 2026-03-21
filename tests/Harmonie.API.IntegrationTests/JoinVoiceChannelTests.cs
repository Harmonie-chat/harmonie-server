using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class JoinVoiceChannelTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public JoinVoiceChannelTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenGuildMemberJoinsVoiceChannel_ShouldReturn200()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Voice Join Guild");
        var voiceChannelId = await GetDefaultVoiceChannelIdAsync(owner.AccessToken, guildId);

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{voiceChannelId}/voice/join",
            owner.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await joinResponse.Content.ReadFromJsonAsync<JoinVoiceChannelResponse>();
        payload.Should().NotBeNull();
        payload!.Token.Should().NotBeNullOrWhiteSpace();
        payload.Url.Should().Be("ws://localhost:7880");
        payload.RoomName.Should().Be($"channel:{voiceChannelId}");
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenChannelIsText_ShouldReturn409()
    {
        var owner = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "text-only-channel");

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{channelId}/voice/join",
            owner.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotVoice);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenUserIsNotGuildMember_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Forbidden Voice Join Guild");
        var voiceChannelId = await GetDefaultVoiceChannelIdAsync(owner.AccessToken, guildId);

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{voiceChannelId}/voice/join",
            outsider.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenChannelDoesNotExist_ShouldReturn404()
    {
        var user = await RegisterAsync();

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{Guid.NewGuid()}/voice/join",
            user.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.PostAsync(
            $"/api/channels/{Guid.NewGuid()}/voice/join",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private async Task<RegisterResponse> RegisterAsync()
    {
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid():N}@harmonie.chat",
            Username: $"user{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        payload.Should().NotBeNull();

        return payload!;
    }

    private async Task<string> CreateGuildAndGetIdAsync(string accessToken, string guildName)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        payload.Should().NotBeNull();

        return payload!.GuildId;
    }

    private async Task<string> CreateChannelAndGetIdAsync(
        string accessToken,
        string name,
        string? guildId = null,
        int position = 0)
    {
        if (guildId is null)
        {
            guildId = await CreateGuildAndGetIdAsync(accessToken, $"Guild for {name}");
        }

        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest(name, ChannelTypeInput.Text, position),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();

        return payload!.ChannelId;
    }

    private async Task<string> GetDefaultVoiceChannelIdAsync(
        string accessToken,
        string guildId)
    {
        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        payload.Should().NotBeNull();

        return payload!.Channels.First(channel => channel.Type == "Voice").ChannelId;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostWithoutBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
