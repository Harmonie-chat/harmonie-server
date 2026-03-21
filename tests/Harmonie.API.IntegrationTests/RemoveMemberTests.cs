using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class RemoveMemberTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RemoveMemberTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RemoveMember_WhenAdminRemovesMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Remove Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveMember_WhenNonAdminTriesToRemove_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var otherMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(otherMember.UserId),
            owner.AccessToken);

        var removeResponse = await SendAuthorizedDeleteAsync(
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
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
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
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await SendAuthorizedDeleteAsync(
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
        var owner = await RegisterAsync();
        var nonMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{nonMember.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
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
