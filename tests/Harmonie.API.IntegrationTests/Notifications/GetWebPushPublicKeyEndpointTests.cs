using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Notifications.GetWebPushPublicKey;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Harmonie.API.IntegrationTests.Notifications;

public sealed class GetWebPushPublicKeyEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;

    public GetWebPushPublicKeyEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWebPushPublicKey_WhenConfigured_ShouldReturnPublicKeyWithoutAuthentication()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["VAPID_PUBLIC_KEY"] = "public-vapid-key",
                    ["VAPID_PRIVATE_KEY"] = "private-vapid-key",
                    ["VAPID_SUBJECT"] = "mailto:contact@harmonie.app"
                });
            });
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/notifications/web-push-public-key",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetWebPushPublicKeyResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        payload.Should().Be(new GetWebPushPublicKeyResponse("public-vapid-key"));
        var rawJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        rawJson.Should().NotContain("private-vapid-key");
    }

    [Fact]
    public async Task GetWebPushPublicKey_WhenPublicKeyIsMissing_ShouldReturnServiceUnavailable()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WebPush:PublicKey"] = string.Empty,
                    ["VAPID_PUBLIC_KEY"] = string.Empty
                });
            });
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/notifications/web-push-public-key",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(
            cancellationToken: TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Notification.WebPushNotConfigured);
    }
}
