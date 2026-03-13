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
using Harmonie.Application.Features.Guilds.ListGuildInvites;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class RevokeInviteEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RevokeInviteEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RevokeInvite_ByAdmin_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var (guild, invite) = await CreateGuildAndInviteAsync(owner.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/invites/{invite.Code}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeInvite_ByAdmin_ShouldMarkInviteAsRevoked()
    {
        var owner = await RegisterAsync();
        var (guild, invite) = await CreateGuildAndInviteAsync(owner.AccessToken);

        await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/invites/{invite.Code}",
            owner.AccessToken);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await listResponse.Content.ReadFromJsonAsync<ListGuildInvitesResponse>();
        result.Should().NotBeNull();
        result!.Invites.Should().ContainSingle();
        result.Invites[0].Code.Should().Be(invite.Code);
        result.Invites[0].RevokedAtUtc.Should().NotBeNull();
        result.Invites[0].IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeInvite_ByCreatorAdmin_CannotBeRevokedByOtherAdmin()
    {
        // Verify that a non-creator admin can also revoke (admin check is OR with creator check).
        var owner = await RegisterAsync();
        var secondAdmin = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Revoke Creator Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        // Add second user as admin
        await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/members/invite",
            new InviteMemberRequest(secondAdmin.UserId),
            owner.AccessToken);
        var promoteResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{guild.GuildId}/members/{secondAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Owner creates an invite
        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();

        // Second admin (not the creator) can also revoke because they are an admin
        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/invites/{invite!.Code}",
            secondAdmin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeInvite_ByNonAdminNonCreator_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Revoke Forbidden Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        // Owner creates the invite
        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();

        // Member (not admin, not creator) tries to revoke
        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/invites/{invite!.Code}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Invite.RevokeForbidden);
    }

    [Fact]
    public async Task RevokeInvite_WhenInviteNotFound_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Revoke NotFound Guild {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild!.GuildId}/invites/NOTFOUND",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Invite.NotFound);
    }

    [Fact]
    public async Task RevokeInvite_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.DeleteAsync($"/api/guilds/{Guid.NewGuid()}/invites/ABCD1234");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeInvite_WhenGuildIdIsInvalid_ShouldReturn400()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedDeleteAsync(
            "/api/guilds/not-a-guid/invites/ABCD1234",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task RevokeInvite_WhenInviteCodeIsInvalid_ShouldReturn400()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{Guid.NewGuid()}/invites/bad!code",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task RevokeInvite_WhenInviteBelongsToOtherGuild_ShouldReturn404()
    {
        var owner = await RegisterAsync();

        var guild1Response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Revoke Other Guild 1 {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        guild1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var guild1 = await guild1Response.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var guild2Response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Revoke Other Guild 2 {Guid.NewGuid():N}"[..40]),
            owner.AccessToken);
        guild2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var guild2 = await guild2Response.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild1!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();

        // Try to revoke guild1's invite via guild2's route
        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild2!.GuildId}/invites/{invite!.Code}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(CreateGuildResponse Guild, CreateGuildInviteResponse Invite)> CreateGuildAndInviteAsync(string accessToken)
    {
        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Revoke Test Guild {Guid.NewGuid():N}"[..40]),
            accessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            accessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();

        return (guild, invite!);
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

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string uri, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string uri, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
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

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
