using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class RemoveMemberTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RemoveMemberTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RemoveMember_WhenAdminRemovesMember_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Remove Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var removeResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveMember_WhenNonAdminTriesToRemove_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var otherMember = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(otherMember.UserId),
            owner.AccessToken);

        var removeResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherMember.UserId}",
            member.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task RemoveMember_WhenNotAuthenticated_ShouldReturn401()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Unauthenticated Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await _client.DeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{member.UserId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTriesToRemoveOwner_ShouldReturn409()
    {
        // The owner is the only Admin in a newly created guild.
        // When the owner (admin) tries to remove themselves, the endpoint
        // must reject with 409 because the owner cannot be removed.
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{owner.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTriesToRemoveNonMember_ShouldReturn404()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var nonMember = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{nonMember.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }
}
