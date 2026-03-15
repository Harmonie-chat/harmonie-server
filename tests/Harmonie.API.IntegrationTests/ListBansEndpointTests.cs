using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.BanMember;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.ListBans;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ListBansEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ListBansEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListBans_WhenAdminAndNoBans_ShouldReturnEmptyList()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await RegisterAsync(token);

        var guild = await CreateGuildAsync($"LBEmpty{token}", owner.AccessToken);

        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListBansResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guild.GuildId);
        payload.Bans.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBans_WhenAdminAndBansExist_ShouldReturnBanList()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await RegisterAsync(token);
        var member = await RegisterAsync(token + "m");

        var guild = await CreateGuildAsync($"LBList{token}", owner.AccessToken);

        await InviteMemberAsync(guild.GuildId, member.UserId, owner.AccessToken);

        // Ban the member
        var banResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId, "Spamming"),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // List bans
        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListBansResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guild.GuildId);
        payload.Bans.Should().HaveCount(1);

        var ban = payload.Bans[0];
        ban.UserId.Should().Be(member.UserId);
        ban.Reason.Should().Be("Spamming");
        ban.BannedBy.Should().Be(owner.UserId);
        ban.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ListBans_WhenNonAdmin_ShouldReturn403()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await RegisterAsync(token);
        var member = await RegisterAsync(token + "m");

        var guild = await CreateGuildAsync($"LBForbid{token}", owner.AccessToken);

        await InviteMemberAsync(guild.GuildId, member.UserId, owner.AccessToken);

        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            member.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task ListBans_WhenGuildNotFound_ShouldReturn404()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await RegisterAsync(token);

        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{Guid.NewGuid()}/bans",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task ListBans_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.GetAsync(
            $"/api/guilds/{Guid.NewGuid()}/bans");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListBans_WhenBanIsRemoved_ShouldNotAppearInList()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await RegisterAsync(token);
        var member = await RegisterAsync(token + "m");

        var guild = await CreateGuildAsync($"LBUnban{token}", owner.AccessToken);

        await InviteMemberAsync(guild.GuildId, member.UserId, owner.AccessToken);

        // Ban the member
        var banResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Unban the member
        var unbanResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/bans/{member.UserId}",
            owner.AccessToken);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // List bans — should be empty
        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListBansResponse>();
        payload.Should().NotBeNull();
        payload!.Bans.Should().BeEmpty();
    }

    private async Task<RegisterResponse> RegisterAsync(string token)
    {
        var request = new RegisterRequest(
            Email: $"test{token}{Guid.NewGuid():N}@harmonie.chat",
            Username: $"u{token}{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<CreateGuildResponse> CreateGuildAsync(string name, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(name),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();
        return guild!;
    }

    private async Task InviteMemberAsync(string guildId, string userId, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/members/invite",
            new InviteMemberRequest(userId),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
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

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
