using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.BanMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UnbanMemberEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UnbanMemberEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnbanMember_WhenAdminUnbansBannedUser_ShouldReturn204()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"UnbanGld{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        // Ban the member first
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId, "Spamming"),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Unban the member
        var unbanResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/bans/{member.UserId}",
            owner.AccessToken);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnbanMember_WhenUnbannedUserCanRejoinViaInvite_ShouldReturn200()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"Rejoin{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        // Ban, then unban
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var unbanResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/bans/{member.UserId}",
            owner.AccessToken);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Create invite and have member rejoin
        var invite = await GuildTestHelper.CreateInviteAsync(_client, guild.GuildId, owner.AccessToken);

        var acceptResponse = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            member.AccessToken);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnbanMember_WhenUserNotBanned_ShouldReturn404()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"NoBan{token}", owner.AccessToken);

        var unbanResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/bans/{member.UserId}",
            owner.AccessToken);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await unbanResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotBanned);
    }

    [Fact]
    public async Task UnbanMember_WhenNonAdmin_ShouldReturn403()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");
        var target = await AuthTestHelper.RegisterAsync(_client, token + "t");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"NoAdmUb{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);
        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, target.UserId, owner.AccessToken);

        // Ban target
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(target.UserId),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Non-admin tries to unban
        var unbanResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/bans/{target.UserId}",
            member.AccessToken);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await unbanResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UnbanMember_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();
        var nonExistentUserId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"/api/guilds/{nonExistentGuildId}/bans/{nonExistentUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
