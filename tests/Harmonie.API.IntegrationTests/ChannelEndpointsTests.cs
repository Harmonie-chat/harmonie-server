using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.UpdateChannel;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ChannelEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ChannelEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateChannel_WhenAdminRenamesChannel_ShouldReturn200()
    {
        var owner = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "original-name");

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "renamed-channel" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updateResponse.Content.ReadFromJsonAsync<UpdateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.ChannelId.Should().Be(channelId);
        payload.Name.Should().Be("renamed-channel");
    }

    [Fact]
    public async Task UpdateChannel_WhenAdminUpdatesPosition_ShouldReturn200()
    {
        var owner = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "position-channel", position: 1);

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { position = 10 },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updateResponse.Content.ReadFromJsonAsync<UpdateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.Position.Should().Be(10);
    }

    [Fact]
    public async Task UpdateChannel_WhenMemberTriesToUpdate_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Forbidden Update Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "member-test-channel", guildId: guildId);

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "hacked-name" },
            member.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UpdateChannel_WhenNonMemberTriesToUpdate_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "outsider-channel");

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "outsider-rename" },
            outsider.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task UpdateChannel_WhenChannelNotFound_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var nonExistentChannelId = Guid.NewGuid();

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{nonExistentChannelId}",
            new { name = "ghost-channel" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task UpdateChannel_WhenNameAlreadyExists_ShouldReturn409()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Name Conflict Guild");

        await CreateChannelInGuildAsync(
            guildId,
            new CreateChannelRequest("taken-name", ChannelTypeInput.Text, 1),
            owner.AccessToken);

        var channelToRenameId = await CreateChannelAndGetIdAsync(
            owner.AccessToken,
            "original-channel",
            guildId: guildId,
            position: 2);

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelToRenameId}",
            new { name = "taken-name" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    [Fact]
    public async Task UpdateChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentChannelId = Guid.NewGuid();

        var updateResponse = await _client.PatchAsJsonAsync(
            $"/api/channels/{nonExistentChannelId}",
            new { name = "anon-rename" });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateChannel_WhenNameIsEmpty_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "valid-name");

        var updateResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
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

    private async Task CreateChannelInGuildAsync(
        string guildId,
        CreateChannelRequest request,
        string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            request,
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
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
}
