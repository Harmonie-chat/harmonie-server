using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

/// <summary>
/// Integration tests for Auth endpoints
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Email.Should().Be(request.Email);
        result.Username.Should().Be(request.Username);
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "invalid-email",
            Username: "testuser",
            Password: "Test123!@#");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        // Arrange - First register a user
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);

        var loginRequest = new LoginRequest(
            EmailOrUsername: registerRequest.Email,
            Password: registerRequest.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Email.Should().Be(registerRequest.Email);
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithUsername_ReturnsOk()
    {
        // Arrange - First register a user
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);

        var loginRequest = new LoginRequest(
            EmailOrUsername: registerRequest.Username,
            Password: registerRequest.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Username.Should().Be(registerRequest.Username);
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange - First register a user to get initial tokens
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        registerPayload.Should().NotBeNull();

        var refreshRequest = new RefreshTokenRequest(registerPayload!.RefreshToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe(registerPayload.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WhenRevokedTokenIsReused_ShouldReturnReuseCodeAndRevokeAssociatedSessionOnly()
    {
        // Arrange
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        registerPayload.Should().NotBeNull();

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(registerRequest.Email, registerRequest.Password),
            TestContext.Current.CancellationToken);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        loginPayload.Should().NotBeNull();

        var firstRefreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(registerPayload!.RefreshToken),
            TestContext.Current.CancellationToken);
        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstRefreshPayload = await firstRefreshResponse.Content.ReadFromJsonAsync<RefreshTokenResponse>(TestContext.Current.CancellationToken);
        firstRefreshPayload.Should().NotBeNull();

        // Act - reuse rotated token
        var reuseResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(registerPayload.RefreshToken),
            TestContext.Current.CancellationToken);

        // Assert - reuse is treated as security incident
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var reuseError = await reuseResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        reuseError.Should().NotBeNull();
        reuseError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);

        // The associated active descendant session is revoked
        var descendantResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(firstRefreshPayload!.RefreshToken),
            TestContext.Current.CancellationToken);
        descendantResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var descendantError = await descendantResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        descendantError.Should().NotBeNull();
        descendantError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);

        // Unrelated active session remains valid
        var unrelatedResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginPayload!.RefreshToken),
            TestContext.Current.CancellationToken);
        unrelatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            EmailOrUsername: "nonexistent@harmonie.chat",
            Password: "WrongPassword123!@#");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var result = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    [Fact]
    public async Task Logout_WithValidSessionToken_ReturnsNoContentAndRevokesRefreshToken()
    {
        // Arrange - First register a user to get session tokens
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        registerPayload.Should().NotBeNull();

        // Act - Logout current session
        var logoutResponse = await _client.SendAuthorizedPostAsync(
            "/api/auth/logout",
            new LogoutRequest(registerPayload!.RefreshToken),
            registerPayload.AccessToken);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(registerPayload.RefreshToken),
            TestContext.Current.CancellationToken);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshError = await refreshResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        refreshError.Should().NotBeNull();
        refreshError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);
    }

    [Fact]
    public async Task Logout_WithRefreshTokenFromAnotherUser_ReturnsUnauthorized()
    {
        // Arrange
        var userARequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var userBRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var userAResponse = await _client.PostAsJsonAsync("/api/auth/register", userARequest, TestContext.Current.CancellationToken);
        var userBResponse = await _client.PostAsJsonAsync("/api/auth/register", userBRequest, TestContext.Current.CancellationToken);
        userAResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        userBResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var userAPayload = await userAResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        var userBPayload = await userBResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        userAPayload.Should().NotBeNull();
        userBPayload.Should().NotBeNull();

        // Act - user A tries to revoke user B refresh token
        var logoutResponse = await _client.SendAuthorizedPostAsync(
            "/api/auth/logout",
            new LogoutRequest(userBPayload!.RefreshToken),
            userAPayload!.AccessToken);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await logoutResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidRefreshToken);
    }

    [Fact]
    public async Task Logout_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LogoutRequest("any_refresh_token");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/logout", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutAll_WithValidAuthentication_ReturnsNoContentAndRevokesAllRefreshTokens()
    {
        // Arrange
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        registerPayload.Should().NotBeNull();

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(registerRequest.Email, registerRequest.Password),
            TestContext.Current.CancellationToken);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        loginPayload.Should().NotBeNull();

        // Act
        var logoutAllResponse = await _client.SendAuthorizedPostNoBodyAsync(
            "/api/auth/logout-all",
            registerPayload!.AccessToken);

        // Assert
        logoutAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshWithRegisterTokenResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(registerPayload.RefreshToken),
            TestContext.Current.CancellationToken);
        refreshWithRegisterTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshWithRegisterTokenError =
            await refreshWithRegisterTokenResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        refreshWithRegisterTokenError.Should().NotBeNull();
        refreshWithRegisterTokenError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);

        var refreshWithLoginTokenResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginPayload!.RefreshToken),
            TestContext.Current.CancellationToken);
        refreshWithLoginTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshWithLoginTokenError =
            await refreshWithLoginTokenResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        refreshWithLoginTokenError.Should().NotBeNull();
        refreshWithLoginTokenError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);
    }

    [Fact]
    public async Task LogoutAll_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/logout-all", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithEmptyRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        registerPayload.Should().NotBeNull();

        // Act
        var response = await _client.SendAuthorizedPostAsync(
            "/api/auth/logout",
            new LogoutRequest(string.Empty),
            registerPayload!.AccessToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task Register_WithoutAvatarAndTheme_ReturnsNullAvatarAndDefaultTheme()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Avatar.Should().BeNull();
        result.Theme.Should().Be("default");
    }

    [Fact]
    public async Task Register_WithFullAvatarAndTheme_ReturnsAvatarAndTheme()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#",
            Avatar: new AvatarAppearanceDto("#ff0000", "star", "#0000ff"),
            Theme: "dark");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Avatar.Should().NotBeNull();
        result.Avatar!.Color.Should().Be("#ff0000");
        result.Avatar.Icon.Should().Be("star");
        result.Avatar.Bg.Should().Be("#0000ff");
        result.Theme.Should().Be("dark");
    }

    [Fact]
    public async Task Register_WithPartialAvatar_ReturnsPartialAvatarAndDefaultTheme()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#",
            Avatar: new AvatarAppearanceDto("#ff0000", null, null));

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Avatar.Should().NotBeNull();
        result.Avatar!.Color.Should().Be("#ff0000");
        result.Avatar.Icon.Should().BeNull();
        result.Avatar.Bg.Should().BeNull();
        result.Theme.Should().Be("default");
    }
}
