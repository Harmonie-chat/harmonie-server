using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.ReorderChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ReorderChannelsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ReorderChannelsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ReorderChannels_WhenAdminReordersChannels_ShouldReturn200WithNewPositions()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Guild");

        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "reorder-ch1", guildId, position: 10);
        var ch2 = await CreateChannelAndGetIdAsync(owner.AccessToken, "reorder-ch2", guildId, position: 11);

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch2, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await reorderResponse.Content.ReadFromJsonAsync<ReorderChannelsResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guildId);

        var reorderedCh1 = payload.Channels.First(c => c.ChannelId == ch1);
        var reorderedCh2 = payload.Channels.First(c => c.ChannelId == ch2);
        reorderedCh1.Position.Should().Be(1);
        reorderedCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReorderChannels_WhenVerifiedByGet_ShouldPersistNewOrder()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Persist Guild");

        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "persist-ch1", guildId, position: 10);
        var ch2 = await CreateChannelAndGetIdAsync(owner.AccessToken, "persist-ch2", guildId, position: 11);

        await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch2, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);

        var getResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            owner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channels = await getResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channels.Should().NotBeNull();

        var getCh1 = channels!.Channels.First(c => c.ChannelId == ch1);
        var getCh2 = channels.Channels.First(c => c.ChannelId == ch2);
        getCh1.Position.Should().Be(1);
        getCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReorderChannels_WhenNonAdminAttempts_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder NonAdmin Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "noadmin-ch1", guildId, position: 0);

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch1, 5)
            ]),
            member.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReorderChannels_WhenChannelNotInGuild_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder NotFound Guild");

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(Guid.NewGuid().ToString(), 0)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReorderChannels_WhenEmptyList_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Empty Guild");

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReorderChannels_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.PatchAsync(
            $"/api/guilds/{Guid.NewGuid()}/channels/reorder",
            JsonContent.Create(new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(Guid.NewGuid().ToString(), 0)
            ])));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderChannels_WhenDuplicateChannelId_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Dup Guild");
        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "dup-ch1", guildId, position: 0);

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch1, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private async Task InviteMemberAsync(string guildId, string userId, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/members/invite",
            new InviteMemberRequest(userId),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

    private async Task<HttpResponseMessage> SendAuthorizedPatchAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
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
