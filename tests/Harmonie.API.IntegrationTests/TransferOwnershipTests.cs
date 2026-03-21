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
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class TransferOwnershipTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public TransferOwnershipTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(member.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_WhenNonOwnerTriesToTransfer_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var otherMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Owner Transfer Guild"),
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

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(otherMember.UserId),
            member.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToSelf_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Self Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/owner/transfer",
            new TransferOwnershipRequest(owner.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerTransferToSelf);
    }

    [Fact]
    public async Task TransferOwnership_WhenNewOwnerIsNotMember_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var nonMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/owner/transfer",
            new TransferOwnershipRequest(nonMember.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var target = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId),
            user.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenNotAuthenticated_ShouldReturn401()
    {
        var target = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId));
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
