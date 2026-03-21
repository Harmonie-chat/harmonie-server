using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.BanMember;
using Harmonie.Application.Features.Guilds.ListBans;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ListBansEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ListBansEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListBans_WhenAdminAndNoBans_ShouldReturnEmptyList()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"LBEmpty{token}", owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"LBList{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        // Ban the member
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId, "Spamming"),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // List bans
        var response = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"LBForbid{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client, token);

        var response = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"LBUnban{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        // Ban the member
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Unban the member
        var unbanResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/bans/{member.UserId}",
            owner.AccessToken);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // List bans — should be empty
        var response = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListBansResponse>();
        payload.Should().NotBeNull();
        payload!.Bans.Should().BeEmpty();
    }
}
