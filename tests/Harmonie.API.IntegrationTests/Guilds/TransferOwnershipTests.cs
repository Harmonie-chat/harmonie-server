using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class TransferOwnershipTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TransferOwnershipTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToMember_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var transferResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(member.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_WhenNonOwnerTriesToTransfer_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var otherMember = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Owner Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload.GuildId, owner.AccessToken, otherMember.AccessToken);

        var transferResponse = await _client.SendAuthorizedPostAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Self Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await _client.SendAuthorizedPostAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var nonMember = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await _client.SendAuthorizedPostAsync(
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
        var user = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await _client.SendAuthorizedPostAsync(
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
        var target = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId));
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
