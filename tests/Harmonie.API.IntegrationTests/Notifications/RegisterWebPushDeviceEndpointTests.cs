using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Infrastructure.Persistence.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests.Notifications;

public sealed class RegisterWebPushDeviceEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RegisterWebPushDeviceEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterWebPushDevice_WithValidSubscription_ShouldStoreNotificationDevice()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var endpoint = $"https://push.example/subscriptions/{Guid.NewGuid():N}";

        var response = await _client.SendAuthorizedPutAsync(
            "/api/notifications/push-subscriptions",
            new
            {
                endpoint,
                expirationTime = (long?)null,
                keys = new
                {
                    p256dh = "p256dh-key",
                    auth = "auth-secret"
                }
            },
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await GetStoredDeviceAsync(endpoint);
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(user.UserId);
        stored.Platform.Should().Be("web_push");
        stored.Token.Should().Be(endpoint);
        stored.WebPushP256dh.Should().Be("p256dh-key");
        stored.WebPushAuth.Should().Be("auth-secret");
    }

    [Fact]
    public async Task RegisterWebPushDevice_WhenEndpointAlreadyExists_ShouldUpdateExistingDevice()
    {
        var firstUser = await AuthTestHelper.RegisterAsync(_client);
        var secondUser = await AuthTestHelper.RegisterAsync(_client);
        var endpoint = $"https://push.example/subscriptions/{Guid.NewGuid():N}";

        var firstResponse = await _client.SendAuthorizedPutAsync(
            "/api/notifications/push-subscriptions",
            CreateRequest(endpoint, "old-p256dh", "old-auth"),
            firstUser.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await _client.SendAuthorizedPutAsync(
            "/api/notifications/push-subscriptions",
            CreateRequest(endpoint, "new-p256dh", "new-auth"),
            secondUser.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var count = await CountStoredDevicesAsync(endpoint);
        count.Should().Be(1);

        var stored = await GetStoredDeviceAsync(endpoint);
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(secondUser.UserId);
        stored.WebPushP256dh.Should().Be("new-p256dh");
        stored.WebPushAuth.Should().Be("new-auth");
    }

    [Fact]
    public async Task RegisterWebPushDevice_WithInvalidEndpoint_ShouldReturnBadRequest()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPutAsync(
            "/api/notifications/push-subscriptions",
            CreateRequest("http://push.example/subscriptions/not-secure", "p256dh", "auth"),
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterWebPushDevice_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/notifications/push-subscriptions",
            CreateRequest($"https://push.example/subscriptions/{Guid.NewGuid():N}", "p256dh", "auth"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static object CreateRequest(string endpoint, string p256dh, string auth)
        => new
        {
            endpoint,
            expirationTime = (long?)null,
            keys = new
            {
                p256dh,
                auth
            }
        };

    // TODO: Replace this direct SQL read with production repository methods once the notification
    // dispatch worker needs query methods for registered devices.
    private async Task<StoredNotificationDevice?> GetStoredDeviceAsync(string endpoint)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT user_id, platform, token, web_push_p256dh, web_push_auth
                              FROM notification_devices
                              WHERE platform = 'web_push' AND token = @Token
                              """;
        command.Parameters.AddWithValue("Token", endpoint);

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        if (!await reader.ReadAsync(TestContext.Current.CancellationToken))
            return null;

        return new StoredNotificationDevice(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4));
    }

    // TODO: Replace this direct SQL read with production repository methods once the notification
    // dispatch worker needs query methods for registered devices.
    private async Task<long> CountStoredDevicesAsync(string endpoint)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(*)
                              FROM notification_devices
                              WHERE platform = 'web_push' AND token = @Token
                              """;
        command.Parameters.AddWithValue("Token", endpoint);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        result.Should().BeOfType<long>();
        return (long)result;
    }

    private sealed record StoredNotificationDevice(
        Guid UserId,
        string Platform,
        string Token,
        string WebPushP256dh,
        string WebPushAuth);
}
