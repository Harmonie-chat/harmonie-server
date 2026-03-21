using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UpdateMemberRoleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UpdateMemberRoleTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminPromotesMember_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Promote Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRoleResponse = await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminDemotesAdmin_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var otherAdmin = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Demote Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(otherAdmin.UserId),
            owner.AccessToken);

        await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);

        var demoteResponse = await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Member),
            owner.AccessToken);
        demoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenNonAdminTriesToChangeRole_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Role Guild"),
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
            new InviteMemberRequest(target.UserId),
            owner.AccessToken);

        var updateRoleResponse = await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{target.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            member.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminTriesToChangeOwnerRole_ShouldReturn409()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var updateRoleResponse = await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{owner.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Member),
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentGuildId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var updateRoleResponse = await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{nonExistentGuildId}/members/{targetId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            user.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var updateRoleResponse = await _client.PutAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/members/{targetId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin));
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenInvalidRole_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invalid Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        var updateRoleResponse = await SendAuthorizedPutRawAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}/role",
            """{"role":"Owner"}""",
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutRawAsync(
        string uri,
        string json,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
