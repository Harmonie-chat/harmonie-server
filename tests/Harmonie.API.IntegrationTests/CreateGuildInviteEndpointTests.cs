using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class CreateGuildInviteEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CreateGuildInviteEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGuildInvite_WithValidRequest_ShouldReturn201()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invite Link Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 10, ExpiresInHours: 24),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();
        invite!.GuildId.Should().Be(guild.GuildId);
        invite.CreatorId.Should().Be(owner.UserId);
        invite.MaxUses.Should().Be(10);
        invite.UsesCount.Should().Be(0);
        invite.ExpiresAtUtc.Should().NotBeNull();
        invite.Code.Should().HaveLength(8);
        invite.InviteId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateGuildInvite_WithNoLimits_ShouldReturn201WithNulls()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Unlimited Invite Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();
        invite!.MaxUses.Should().BeNull();
        invite.ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CreateGuildInvite_WhenNotAdmin_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Member Invite Attempt Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteMemberResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteMemberResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(),
            member.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task CreateGuildInvite_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentGuildId = Guid.NewGuid();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{nonExistentGuildId}/invites",
            new CreateGuildInviteRequest(),
            user.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task CreateGuildInvite_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();

        var inviteResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/invites",
            new CreateGuildInviteRequest());
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateGuildInvite_WhenMaxUsesIsZero_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Zero MaxUses Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 0),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task CreateGuildInvite_WhenExpiresInHoursIsZero_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Zero Expires Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(ExpiresInHours: 0),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task CreateGuildInvite_WhenNegativeMaxUses_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Negative MaxUses Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: -5),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
