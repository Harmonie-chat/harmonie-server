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

public sealed class LeaveGuildTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public LeaveGuildTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LeaveGuild_WhenMemberLeaves_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/leave",
            member.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LeaveGuild_WhenOwnerLeaves_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            owner.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotLeave);
    }

    [Fact]
    public async Task LeaveGuild_WhenNotMember_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Not Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            outsider.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task LeaveGuild_WhenNotAuthenticated_ShouldReturn401()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Auth Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await _client.PostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaveGuild_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{nonExistentGuildId}/leave",
            user.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
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

    private async Task<HttpResponseMessage> SendAuthorizedPostNoBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
