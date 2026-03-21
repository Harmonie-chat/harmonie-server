using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.UpdateUserStatus;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UserStatusTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UserStatusTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateUserStatus_WithValidStatus_ShouldUpdateAndReturnStatus()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "dnd" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateUserStatusResponse>();
        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(user.UserId);
        payload.Status.Should().Be("dnd");

        var profileResponse = await SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Status.Should().Be("dnd");
    }

    [Fact]
    public async Task UpdateUserStatus_WithInvisible_ShouldPersistInvisible()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "invisible" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateUserStatusResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("invisible");

        // Verify persistence via GET profile
        var profileResponse = await SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Status.Should().Be("invisible");
    }

    [Fact]
    public async Task UpdateUserStatus_WithInvalidStatus_ShouldReturnValidationError()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "away" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task UpdateUserStatus_WithEmptyStatus_ShouldReturnValidationError()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task UpdateUserStatus_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/users/me/status",
            new { status = "online" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserStatus_AllValidStatuses_ShouldSucceed()
    {
        var user = await RegisterAsync();

        foreach (var status in new[] { "online", "idle", "dnd", "invisible" })
        {
            var response = await SendAuthorizedPutAsync(
                "/api/users/me/status",
                new { status },
                user.AccessToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<UpdateUserStatusResponse>();
            payload.Should().NotBeNull();
            payload!.Status.Should().Be(status);
        }
    }

    [Fact]
    public async Task GetMyProfile_ShouldReturnDefaultOnlineStatus()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<GetMyProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Status.Should().Be("online");
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

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
