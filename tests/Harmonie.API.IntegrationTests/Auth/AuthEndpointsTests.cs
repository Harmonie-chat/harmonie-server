using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Harmonie.Application.Features.Auth.Login;
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
        GetIssuedRefreshCookie(response).Should().NotBeEmpty();

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Email.Should().Be(request.Email);
        result.Username.Should().Be(request.Username);
        result.AccessToken.Should().NotBeNullOrEmpty();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().NotContain("\"refreshToken\"");
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
        GetIssuedRefreshCookie(response).Should().NotBeEmpty();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Email.Should().Be(registerRequest.Email);
        result.AccessToken.Should().NotBeNullOrEmpty();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().NotContain("\"refreshToken\"");
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
    public async Task RefreshToken_WithCookie_ReturnsNewAccessToken()
    {
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            registerRequest,
            TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await SendRefreshAsync(GetIssuedRefreshCookie(registerResponse));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().NotContain("\"refreshToken\"");
    }

    [Fact]
    public async Task RefreshToken_WithCookie_RotatesRefreshCookie()
    {
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            registerRequest,
            TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var initialCookie = GetIssuedRefreshCookie(registerResponse);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={initialCookie}");

        var refreshResponse = await _client.SendAsync(
            refreshRequest,
            TestContext.Current.CancellationToken);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedCookie = GetIssuedRefreshCookie(refreshResponse);
        rotatedCookie.Should().NotBe(initialCookie);
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
        var registerCookie = GetIssuedRefreshCookie(registerResponse);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(registerRequest.Email, registerRequest.Password),
            TestContext.Current.CancellationToken);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginCookie = GetIssuedRefreshCookie(loginResponse);

        var firstRefreshResponse = await SendRefreshAsync(registerCookie);
        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstRefreshCookie = GetIssuedRefreshCookie(firstRefreshResponse);

        // Act - reuse rotated token
        var reuseResponse = await SendRefreshAsync(registerCookie);

        // Assert - reuse is treated as security incident
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var reuseError = await reuseResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        reuseError.Should().NotBeNull();
        reuseError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);

        // The associated active descendant session is revoked
        var descendantResponse = await SendRefreshAsync(firstRefreshCookie);
        descendantResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var descendantError = await descendantResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        descendantError.Should().NotBeNull();
        descendantError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);

        // Unrelated active session remains valid
        var unrelatedResponse = await SendRefreshAsync(loginCookie);
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
        var refreshCookie = GetIssuedRefreshCookie(registerResponse);

        // Act - Logout current session
        var logoutResponse = await SendLogoutAsync(refreshCookie, registerPayload!.AccessToken);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await SendRefreshAsync(refreshCookie);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshError = await refreshResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        refreshError.Should().NotBeNull();
        refreshError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);
    }

    [Fact]
    public async Task Logout_WithCookie_DeletesRefreshCookie()
    {
        var registerRequest = new RegisterRequest(
            Email: $"test{Guid.NewGuid()}@harmonie.chat",
            Username: $"testuser{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            registerRequest,
            TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>(
            TestContext.Current.CancellationToken);
        registerPayload.Should().NotBeNull();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Authorization = new("Bearer", registerPayload!.AccessToken);
        logoutRequest.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={GetIssuedRefreshCookie(registerResponse)}");

        var logoutResponse = await _client.SendAsync(
            logoutRequest,
            TestContext.Current.CancellationToken);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deletedCookie = GetRefreshCookieHeader(logoutResponse);
        deletedCookie.Should().Contain($"{RefreshTokenCookie.Name}=");
        deletedCookie.ToLowerInvariant().Should().Contain("expires=thu, 01 jan 1970 00:00:00 gmt");
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
        userAPayload.Should().NotBeNull();

        // Act - user A tries to revoke user B refresh token
        var logoutResponse = await SendLogoutAsync(
            GetIssuedRefreshCookie(userBResponse),
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
        // Act
        var response = await _client.PostAsync(
            "/api/auth/logout",
            content: null,
            TestContext.Current.CancellationToken);

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
        var registerCookie = GetIssuedRefreshCookie(registerResponse);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(registerRequest.Email, registerRequest.Password),
            TestContext.Current.CancellationToken);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginCookie = GetIssuedRefreshCookie(loginResponse);

        // Act
        var logoutAllResponse = await _client.SendAuthorizedPostNoBodyAsync(
            "/api/auth/logout-all",
            registerPayload!.AccessToken);

        // Assert
        logoutAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshWithRegisterTokenResponse = await SendRefreshAsync(registerCookie);
        refreshWithRegisterTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshWithRegisterTokenError =
            await refreshWithRegisterTokenResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        refreshWithRegisterTokenError.Should().NotBeNull();
        refreshWithRegisterTokenError!.Code.Should().Be(ApplicationErrorCodes.Auth.RefreshTokenReuseDetected);

        var refreshWithLoginTokenResponse = await SendRefreshAsync(loginCookie);
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
    public async Task Logout_WithoutRefreshCookie_ReturnsBadRequest()
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
        var response = await _client.SendAuthorizedPostNoBodyAsync(
            "/api/auth/logout",
            registerPayload!.AccessToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    private async Task<HttpResponseMessage> SendRefreshAsync(string refreshCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={refreshCookie}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendLogoutAsync(string refreshCookie, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={refreshCookie}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string GetIssuedRefreshCookie(HttpResponseMessage response)
    {
        var header = GetRefreshCookieHeader(response);
        var normalizedHeader = header.ToLowerInvariant();
        normalizedHeader.Should().Contain("httponly");
        normalizedHeader.Should().Contain("secure");
        normalizedHeader.Should().Contain("samesite=none");
        normalizedHeader.Should().Contain("path=/");

        return header.Split(';', 2)[0].Split('=', 2)[1];
    }

    private static string GetRefreshCookieHeader(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{RefreshTokenCookie.Name}=", StringComparison.Ordinal));

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

    [Fact]
    public async Task Register_ConcurrentRequestsWithSameIdentity_ShouldCreateSingleUser()
    {
        // Race safety net: the losers must get a clean duplicate error, never a 500
        var token = Guid.NewGuid().ToString("N")[..12];
        var request = new RegisterRequest(
            Email: $"race{token}@harmonie.chat",
            Username: $"race{token}",
            Password: "Test123!@#");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => _client.PostAsJsonAsync("/api/auth/register", request)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);

        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            loser.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var error = await loser.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
            error.Should().NotBeNull();
            error!.Code.Should().BeOneOf(
                ApplicationErrorCodes.Auth.DuplicateEmail,
                ApplicationErrorCodes.Auth.DuplicateUsername);
        }
    }
}
