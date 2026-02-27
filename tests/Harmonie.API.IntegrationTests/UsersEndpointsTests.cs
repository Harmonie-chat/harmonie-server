using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Users.GetMyProfile;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UsersEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;

    public UsersEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _configuration = factory.Services.GetRequiredService<IConfiguration>();
    }

    [Fact]
    public async Task GetMyProfile_WithValidAuthentication_ShouldReturnProfile()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedGetAsync("/api/users/me", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        payload.Should().NotBeNull();

        payload!.UserId.Should().Be(user.UserId);
        payload.Username.Should().Be(user.Username);
        payload.DisplayName.Should().BeNull();
        payload.Bio.Should().BeNull();
        payload.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetMyProfile_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyProfile_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var accessToken = BuildAccessToken(Guid.NewGuid().ToString());

        var response = await SendAuthorizedGetAsync("/api/users/me", accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
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

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string uri, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
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
