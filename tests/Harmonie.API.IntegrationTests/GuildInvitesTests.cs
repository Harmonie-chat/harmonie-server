using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildInvitesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public GuildInvitesTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGuildMembers_WhenRequesterIsMember_ShouldReturnGuildMembers()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Members Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members",
            member.AccessToken);
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersPayload = await membersResponse.Content.ReadFromJsonAsync<GetGuildMembersResponse>();
        membersPayload.Should().NotBeNull();

        membersPayload!.GuildId.Should().Be(createGuildPayload.GuildId);
        membersPayload.Members.Should().HaveCount(2);
        membersPayload.Members.Should().Contain(x => x.UserId == owner.UserId && x.Role == "Admin");
        membersPayload.Members.Should().Contain(x => x.UserId == member.UserId && x.Role == "Member");
    }

    [Fact]
    public async Task InviteMember_WhenNonAdminInvites_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var target = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(target.UserId),
            outsider.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();

        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task InviteMember_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Missing Invite Target Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(Guid.NewGuid().ToString()),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteTargetNotFound);
    }

    [Fact]
    public async Task InviteMember_WhenTargetUserIsAlreadyMember_ShouldReturnConflict()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Duplicate Invite Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var firstInviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        firstInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondInviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        secondInviteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await secondInviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
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

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
