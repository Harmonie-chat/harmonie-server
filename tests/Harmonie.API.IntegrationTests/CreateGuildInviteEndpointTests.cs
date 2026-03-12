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
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class CreateGuildInviteEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CreateGuildInviteEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGuildInvite_WithValidRequest_ShouldReturn201()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invite Link Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
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
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Unlimited Invite Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await SendAuthorizedPostAsync(
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
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Member Invite Attempt Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteMemberResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteMemberResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var inviteResponse = await SendAuthorizedPostAsync(
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
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var inviteResponse = await SendAuthorizedPostAsync(
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
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Zero MaxUses Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await SendAuthorizedPostAsync(
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
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Zero Expires Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await SendAuthorizedPostAsync(
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
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Negative MaxUses Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: -5),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
}
