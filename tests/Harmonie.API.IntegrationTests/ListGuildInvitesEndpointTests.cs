using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.ListGuildInvites;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ListGuildInvitesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ListGuildInvitesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListGuildInvites_WithNoInvites_ShouldReturn200WithEmptyList()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"List Invites Empty Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await listResponse.Content.ReadFromJsonAsync<ListGuildInvitesResponse>();
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guild.GuildId);
        result.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task ListGuildInvites_WithMultipleInvites_ShouldReturnAll()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Multi Invites Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var invite1Response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 5, ExpiresInHours: 24),
            owner.AccessToken);
        invite1Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite2Response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        invite2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await listResponse.Content.ReadFromJsonAsync<ListGuildInvitesResponse>();
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guild.GuildId);
        result.Invites.Should().HaveCount(2);
        result.Invites.Should().AllSatisfy(i =>
        {
            i.Code.Should().HaveLength(8);
            i.CreatorId.Should().Be(owner.UserId);
            i.UsesCount.Should().Be(0);
            i.IsExpired.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ListGuildInvites_ShouldIncludeIsExpiredFlag()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Expired Flag Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        // Valid invite (no limits)
        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await listResponse.Content.ReadFromJsonAsync<ListGuildInvitesResponse>();
        result.Should().NotBeNull();
        result!.Invites.Should().ContainSingle();
        result.Invites[0].IsExpired.Should().BeFalse();
        result.Invites[0].ExpiresAtUtc.Should().BeNull();
        result.Invites[0].MaxUses.Should().BeNull();
    }

    [Fact]
    public async Task ListGuildInvites_WhenNotAdmin_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Admin Only Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var inviteMemberResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteMemberResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            member.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await listResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task ListGuildInvites_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{nonExistentGuildId}/invites",
            user.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await listResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task ListGuildInvites_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();

        var listResponse = await _client.GetAsync($"/api/guilds/{nonExistentGuildId}/invites");
        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListGuildInvites_WhenGuildIdIsInvalid_ShouldReturn400()
    {
        var user = await RegisterAsync();

        var listResponse = await SendAuthorizedGetAsync(
            "/api/guilds/not-a-guid/invites",
            user.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await listResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task ListGuildInvites_ShouldReturnInvitesOrderedByCreatedAtDescending()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Ordered Invites Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var first = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 1),
            owner.AccessToken);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstInvite = await first.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        var second = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 2),
            owner.AccessToken);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondInvite = await second.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await listResponse.Content.ReadFromJsonAsync<ListGuildInvitesResponse>();
        result.Should().NotBeNull();
        result!.Invites.Should().HaveCount(2);
        // Most recent first
        result.Invites[0].Code.Should().Be(secondInvite!.Code);
        result.Invites[1].Code.Should().Be(firstInvite!.Code);
    }

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

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string uri, string accessToken)
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
}
