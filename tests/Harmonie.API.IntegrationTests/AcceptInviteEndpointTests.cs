using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.AcceptInvite;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class AcceptInviteEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AcceptInviteEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptInvite_WithValidCode_ShouldReturn200()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var joiner = await AuthTestHelper.RegisterAsync(_client, token + "j");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"AcceptGuild{token}", owner.AccessToken);
        var invite = await GuildTestHelper.CreateInviteAsync(_client, guild.GuildId, owner.AccessToken);

        var response = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            joiner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AcceptInviteResponse>();
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guild.GuildId);
        result.Role.Should().Be("Member");
    }

    [Fact]
    public async Task AcceptInvite_WhenAlreadyMember_ShouldReturn409()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"AlreadyMem{token}", owner.AccessToken);
        var invite = await GuildTestHelper.CreateInviteAsync(_client, guild.GuildId, owner.AccessToken);

        // Owner is already a member
        var response = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    [Fact]
    public async Task AcceptInvite_WhenInviteNotFound_ShouldReturn404()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var user = await AuthTestHelper.RegisterAsync(_client, token);

        var response = await _client.SendAuthorizedPostNoBodyAsync(
            "/api/invites/ZZZZZZZZ/accept",
            user.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Invite.NotFound);
    }

    [Fact]
    public async Task AcceptInvite_WhenUnauthenticated_ShouldReturn401()
    {
        var response = await _client.PostAsync("/api/invites/ABCD1234/accept", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvite_WhenInvalidCodeFormat_ShouldReturn400()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var user = await AuthTestHelper.RegisterAsync(_client, token);

        var response = await _client.SendAuthorizedPostNoBodyAsync(
            "/api/invites/abc/accept",
            user.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task AcceptInvite_WithMaxUsesReached_ShouldReturn410()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var joiner1 = await AuthTestHelper.RegisterAsync(_client, token + "j1");
        var joiner2 = await AuthTestHelper.RegisterAsync(_client, token + "j2");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"MaxUseGuild{token}", owner.AccessToken);
        var invite = await GuildTestHelper.CreateInviteAsync(_client, guild.GuildId, owner.AccessToken, maxUses: 1);

        // First accept should succeed
        var response1 = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            joiner1.AccessToken);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second accept should fail — max uses reached
        var response2 = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            joiner2.AccessToken);
        response2.StatusCode.Should().Be(HttpStatusCode.Gone);

        var error = await response2.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Invite.Exhausted);
    }
}
