using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UserProfileTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;

    public UserProfileTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _configuration = factory.Services.GetRequiredService<IConfiguration>();
    }

    [Fact]
    public async Task GetMyProfile_WithValidAuthentication_ShouldReturnProfile()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        payload.Should().NotBeNull();

        payload!.UserId.Should().Be(user.UserId);
        payload.Username.Should().Be(user.Username);
        payload.DisplayName.Should().BeNull();
        payload.Bio.Should().BeNull();
        payload.AvatarFileId.Should().BeNull();
        payload.Theme.Should().Be("default");
        payload.Language.Should().BeNull();
        payload.Avatar.Should().BeNull();
    }

    [Fact]
    public async Task GetMyProfile_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidCredentials);
        error.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        error.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetMyProfile_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var accessToken = BuildAccessToken(Guid.NewGuid().ToString());

        var response = await _client.SendAuthorizedGetAsync("/api/users/me", accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task UpdateMyProfile_WithPartialUpdate_ShouldUpdateOnlyProvidedField()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var avatarFileId = await UploadTestHelper.UploadFileAsync(_client, user.AccessToken, "avatar-initial.png", "image/png", "initial avatar");

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new
            {
                displayName = "Initial Name",
                bio = "Initial bio",
                avatarFileId
            },
            user.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { displayName = "Updated Name" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(user.UserId);
        payload.Username.Should().Be(user.Username);
        payload.DisplayName.Should().Be("Updated Name");
        payload.Bio.Should().Be("Initial bio");
        payload.AvatarFileId.Should().Be(avatarFileId);

        var getResponse = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await getResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("Updated Name");
        profile.Bio.Should().Be("Initial bio");
        profile.AvatarFileId.Should().Be(avatarFileId);
    }

    [Fact]
    public async Task UpdateMyProfile_WithExplicitNull_ShouldResetFieldToNull()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var avatarFileId = await UploadTestHelper.UploadFileAsync(_client, user.AccessToken, "avatar-reset.png", "image/png", "reset avatar");

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new
            {
                displayName = "Alice",
                bio = "Bio to reset",
                avatarFileId
            },
            user.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new
            {
                bio = (string?)null,
                avatarFileId = (string?)null
            },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.DisplayName.Should().Be("Alice");
        payload.Bio.Should().BeNull();
        payload.AvatarFileId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMyProfile_WithOutOfRangeValues_ShouldReturnStableValidationError()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { displayName = new string('x', 101) },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        error.Status.Should().Be((int)HttpStatusCode.BadRequest);
        error.TraceId.Should().NotBeNullOrWhiteSpace();
        error.Errors.Should().NotBeNull();
        var fieldErrors = error.Errors!;
        fieldErrors.Should().ContainKey("DisplayName");
        fieldErrors["DisplayName"][0].Code.Should().Be(ApplicationErrorCodes.Validation.MaxLength);
    }

    [Fact]
    public async Task UpdateMyProfile_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/users/me",
            new { displayName = "NoAuth" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMyProfile_WithThemeAndLanguage_ShouldUpdateThemeAndLanguage()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { theme = "dark", language = "fr" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.Theme.Should().Be("dark");
        payload.Language.Should().Be("fr");

        var getResponse = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        var profile = await getResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Theme.Should().Be("dark");
        profile.Language.Should().Be("fr");
    }

    [Fact]
    public async Task UpdateMyProfile_WithAvatarAppearance_ShouldReturnNestedAvatarObject()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatar = new { color = "#FFF4D6", icon = "star", bg = "#1F2937" } },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.Avatar.Should().NotBeNull();
        payload.Avatar!.Color.Should().Be("#FFF4D6");
        payload.Avatar.Icon.Should().Be("star");
        payload.Avatar.Bg.Should().Be("#1F2937");

        var getResponse = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        var profile = await getResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Avatar.Should().NotBeNull();
        profile.Avatar!.Color.Should().Be("#FFF4D6");
    }

    [Fact]
    public async Task UpdateMyProfile_WithPartialAvatarAppearance_ShouldKeepOmittedSubFields()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatar = new { color = "#FFF4D6", icon = "star", bg = "#1F2937" } },
            user.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatar = new { color = "#UPDATED" } },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.Avatar.Should().NotBeNull();
        payload.Avatar!.Color.Should().Be("#UPDATED");
        payload.Avatar.Icon.Should().Be("star");
        payload.Avatar.Bg.Should().Be("#1F2937");
    }

    [Fact]
    public async Task UpdateMyProfile_WithNullAvatar_ShouldClearAllAvatarFields()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatar = new { color = "#FFF4D6", icon = "star", bg = "#1F2937" } },
            user.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatar = (object?)null },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.Avatar.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMyProfile_WithExplicitNullLanguage_ShouldClearLanguage()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { language = "fr" },
            user.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { language = (string?)null },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateMyProfileResponse>();
        payload.Should().NotBeNull();
        payload!.Language.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMyAvatar_WithExistingAvatar_ShouldReturnNoContentAndClearProfile()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var avatarFileId = await UploadTestHelper.UploadFileAsync(_client, user.AccessToken, "avatar-delete.png", "image/png", "avatar to delete");

        var seedResponse = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatarFileId },
            user.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.SendAuthorizedDeleteAsync("/api/users/me/avatar", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var profileResponse = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await profileResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.AvatarFileId.Should().BeNull();

        var oldFileResponse = await _client.SendAuthorizedGetAsync($"/api/files/{avatarFileId}", user.AccessToken);
        oldFileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMyAvatar_WhenNoAvatarIsSet_ShouldReturnNotFound()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedDeleteAsync("/api/users/me/avatar", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);
    }

    [Fact]
    public async Task DeleteMyAvatar_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/users/me/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private string BuildAccessToken(string userId)
    {
        var secret = _configuration["Jwt:Secret"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Configuration value 'Jwt:Secret' is missing.");
        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException("Configuration value 'Jwt:Issuer' is missing.");
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configuration value 'Jwt:Audience' is missing.");

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim("sub", userId)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
