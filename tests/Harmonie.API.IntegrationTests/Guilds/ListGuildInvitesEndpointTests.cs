using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
using Harmonie.Application.Features.Guilds.ListGuildInvites;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ListGuildInvitesEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ListGuildInvitesEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListGuildInvites_WithNoInvites_ShouldReturn200WithEmptyList()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"List Invites Empty Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var listResponse = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Multi Invites Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var invite1Response = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 5, ExpiresInHours: 24),
            owner.AccessToken);
        invite1Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite2Response = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        invite2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Expired Flag Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        // Valid invite (no limits)
        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Admin Only Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, guild!.GuildId, owner.AccessToken, member.AccessToken);

        var listResponse = await _client.SendAuthorizedGetAsync(
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
        var user = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentGuildId = Guid.NewGuid();

        var listResponse = await _client.SendAuthorizedGetAsync(
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
        var user = await AuthTestHelper.RegisterAsync(_client);

        var listResponse = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Ordered Invites Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var first = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 1),
            owner.AccessToken);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstInvite = await first.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        var second = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 2),
            owner.AccessToken);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondInvite = await second.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        var listResponse = await _client.SendAuthorizedGetAsync(
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
}
