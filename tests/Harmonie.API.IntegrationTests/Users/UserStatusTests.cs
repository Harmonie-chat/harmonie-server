using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.UpdateUserStatus;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UserStatusTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserStatusTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateUserStatus_WithValidStatus_ShouldUpdateAndReturnStatus()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "dnd" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateUserStatusResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(user.UserId);
        payload.Status.Should().Be("dnd");

        var profileResponse = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>(TestContext.Current.CancellationToken);
        profile.Should().NotBeNull();
        profile!.Status.Should().Be("dnd");
    }

    [Fact]
    public async Task UpdateUserStatus_WithInvisible_ShouldPersistInvisible()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "invisible" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateUserStatusResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("invisible");

        // Verify persistence via GET profile
        var profileResponse = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<GetMyProfileResponse>(TestContext.Current.CancellationToken);
        profile.Should().NotBeNull();
        profile!.Status.Should().Be("invisible");
    }

    [Fact]
    public async Task UpdateUserStatus_WithInvalidStatus_ShouldReturnValidationError()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "away" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task UpdateUserStatus_WithEmptyStatus_ShouldReturnValidationError()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPutAsync(
            "/api/users/me/status",
            new { status = "" },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task UpdateUserStatus_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/users/me/status",
            new { status = "online" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserStatus_AllValidStatuses_ShouldSucceed()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        foreach (var status in new[] { "online", "idle", "dnd", "invisible" })
        {
            var response = await _client.SendAuthorizedPutAsync(
                "/api/users/me/status",
                new { status },
                user.AccessToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<UpdateUserStatusResponse>(TestContext.Current.CancellationToken);
            payload.Should().NotBeNull();
            payload!.Status.Should().Be(status);
        }
    }

    [Fact]
    public async Task GetMyProfile_ShouldReturnDefaultOnlineStatus()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync("/api/users/me", user.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<GetMyProfileResponse>(TestContext.Current.CancellationToken);
        profile.Should().NotBeNull();
        profile!.Status.Should().Be("online");
    }
}
