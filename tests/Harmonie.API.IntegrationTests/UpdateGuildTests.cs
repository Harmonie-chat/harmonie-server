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
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Guilds.UpdateGuild;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UpdateGuildTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public UpdateGuildTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateGuild_WithOwnerAndPartialIconUpdate_ShouldPersistAndKeepOmittedSubFields()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Patchable Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        var iconFileId = await UploadFileAsync(owner.AccessToken, "guild-icon.png", "image/png", "guild icon");

        var seedResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}",
            new
            {
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" },
                iconFileId
            },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedPatchAsync(
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

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
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
        var owner = await RegisterAsync();
        var admin = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Admin Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        var iconFileId = await UploadFileAsync(owner.AccessToken, "admin-guild.png", "image/png", "admin guild icon");

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(admin.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var promoteResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{admin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var seedResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                iconFileId,
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" }
            },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedPatchAsync(
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
        var owner = await RegisterAsync();
        var iconFileId = await UploadFileAsync(owner.AccessToken, "guild-icon-delete.png", "image/png", "guild icon delete");

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Icon Delete"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var seedResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guild!.GuildId}",
            new { iconFileId },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/icon",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
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
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Without Icon"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var response = await SendAuthorizedDeleteAsync(
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
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var iconFileId = await UploadFileAsync(owner.AccessToken, "guild-icon-member.png", "image/png", "guild icon");

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Member Forbidden"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var seedResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guild.GuildId}",
            new { iconFileId },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedDeleteAsync(
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
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Forbidden Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new { name = "Should Fail" },
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
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

    private async Task<HttpResponseMessage> SendAuthorizedPatchAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
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

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<string> UploadFileAsync(
        string accessToken,
        string fileName,
        string contentType,
        string content)
    {
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/files/uploads")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        payload.Should().NotBeNull();
        payload!.FileId.Should().NotBeNullOrWhiteSpace();
        return payload.FileId;
    }
}
