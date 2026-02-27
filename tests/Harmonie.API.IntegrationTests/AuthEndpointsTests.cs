using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

/// <summary>
/// Integration tests for Auth endpoints
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
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
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
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
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<ApplicationError>();
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

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            EmailOrUsername: registerRequest.Email,
            Password: registerRequest.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
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

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            EmailOrUsername: registerRequest.Username,
            Password: registerRequest.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
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

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        registerPayload.Should().NotBeNull();

        var refreshRequest = new RefreshTokenRequest(registerPayload!.RefreshToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        result.Should().NotBeNull();

        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe(registerPayload.RefreshToken);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            EmailOrUsername: "nonexistent@harmonie.chat",
            Password: "WrongPassword123!@#");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var result = await response.Content.ReadFromJsonAsync<ApplicationError>();
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

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        registerPayload.Should().NotBeNull();

        // Act - Logout current session
        var logoutResponse = await SendAuthorizedPostAsync(
            "/api/auth/logout",
            new LogoutRequest(registerPayload!.RefreshToken),
            registerPayload.AccessToken);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(registerPayload.RefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshError = await refreshResponse.Content.ReadFromJsonAsync<ApplicationError>();
        refreshError.Should().NotBeNull();
        refreshError!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidRefreshToken);
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

        var userAResponse = await _client.PostAsJsonAsync("/api/auth/register", userARequest);
        var userBResponse = await _client.PostAsJsonAsync("/api/auth/register", userBRequest);
        userAResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        userBResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var userAPayload = await userAResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        var userBPayload = await userBResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        userAPayload.Should().NotBeNull();
        userBPayload.Should().NotBeNull();

        // Act - user A tries to revoke user B refresh token
        var logoutResponse = await SendAuthorizedPostAsync(
            "/api/auth/logout",
            new LogoutRequest(userBPayload!.RefreshToken),
            userAPayload!.AccessToken);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await logoutResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidRefreshToken);
    }

    [Fact]
    public async Task Logout_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LogoutRequest("any_refresh_token");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/logout", request);

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

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        registerPayload.Should().NotBeNull();

        // Act
        var response = await SendAuthorizedPostAsync(
            "/api/auth/logout",
            new LogoutRequest(string.Empty),
            registerPayload!.AccessToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
