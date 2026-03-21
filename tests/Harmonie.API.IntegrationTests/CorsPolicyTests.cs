using Harmonie.API.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class CorsPolicyTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsPolicyTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PreflightRequest_InDevelopment_ShouldAllowAnyOriginWithCredentials()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).Should().BeTrue();
        origins.Should().Contain("http://localhost:3000");

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials).Should().BeTrue();
        credentials.Should().Contain("true");
    }
}
