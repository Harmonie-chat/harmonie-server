using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Guilds.UpdateGuild;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UpdateGuildTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UpdateGuildTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateGuild_WithOwnerAndPartialIconUpdate_ShouldPersistAndKeepOmittedSubFields()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Patchable Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        var iconFileId = await UploadTestHelper.UploadFileAsync(_client, owner.AccessToken, "guild-icon.png", "image/png", "guild icon");

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}",
            new
            {
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" },
                iconFileId
            },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                name = "Renamed Guild",
                icon = new { color = "#F59E0B" }
            },
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateGuildResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(createGuildPayload.GuildId);
        payload.Name.Should().Be("Renamed Guild");
        payload.IconFileId.Should().Be(iconFileId);
        payload.Icon.Should().NotBeNull();
        payload.Icon!.Color.Should().Be("#F59E0B");
        payload.Icon.Name.Should().Be("sword");
        payload.Icon.Bg.Should().Be("#1F2937");

        var listResponse = await _client.SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Guilds.Should().Contain(guild =>
            guild.GuildId == createGuildPayload.GuildId
            && guild.Name == "Renamed Guild"
            && guild.IconFileId == iconFileId
            && guild.Icon != null
            && guild.Icon.Color == "#F59E0B"
            && guild.Icon.Name == "sword"
            && guild.Icon.Bg == "#1F2937");
    }

    [Fact]
    public async Task UpdateGuild_WithAdminAndNullIcon_ShouldClearIconFields()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var admin = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Admin Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        var iconFileId = await UploadTestHelper.UploadFileAsync(_client, owner.AccessToken, "admin-guild.png", "image/png", "admin guild icon");

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, admin.AccessToken);

        var promoteResponse = await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{admin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                iconFileId,
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" }
            },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                iconFileId = (string?)null,
                icon = (object?)null
            },
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateGuildResponse>();
        payload.Should().NotBeNull();
        payload!.IconFileId.Should().BeNull();
        payload.Icon.Should().BeNull();
    }

    [Fact]
    public async Task DeleteGuildIcon_WithOwnerAndExistingIcon_ShouldReturnNoContentAndClearGuildIcon()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var iconFileId = await UploadTestHelper.UploadFileAsync(_client, owner.AccessToken, "guild-icon-delete.png", "image/png", "guild icon delete");

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Icon Delete"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guild!.GuildId}",
            new { iconFileId },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/icon",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Guilds.Should().Contain(item =>
            item.GuildId == guild.GuildId &&
            item.IconFileId == null);
    }

    [Fact]
    public async Task DeleteGuildIcon_WhenGuildHasNoIcon_ShouldReturnNotFound()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Without Icon"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild!.GuildId}/icon",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);
    }

    [Fact]
    public async Task DeleteGuildIcon_WhenCallerIsMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var iconFileId = await UploadTestHelper.UploadFileAsync(_client, owner.AccessToken, "guild-icon-member.png", "image/png", "guild icon");

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Member Forbidden"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, guild!.GuildId, owner.AccessToken, member.AccessToken);

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guild.GuildId}",
            new { iconFileId },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/icon",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UpdateGuild_WhenCallerIsRegularMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Forbidden Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new { name = "Should Fail" },
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }
}
