using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests.Middleware;

/// <summary>
/// Integration tests for TraceIdMiddleware
/// </summary>
public sealed class TraceIdMiddlewareTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TraceIdMiddlewareTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task XTraceIdHeader_PresentInResponse()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Headers.TryGetValues("X-Trace-Id", out var traceIdValues);
        traceIdValues.Should().NotBeNull();
        var traceId = traceIdValues?.FirstOrDefault() ?? string.Empty;
        traceId.Should().NotBeNullOrEmpty();
        traceId.Length.Should().Be(32);
        traceId.Should().BeEquivalentTo(traceId.ToLowerInvariant()); // hex lowercase
    }

    [Fact]
    public async Task XTraceIdHeader_DifferentForEachRequest()
    {
        // Act
        var response1 = await _client.GetAsync("/health");
        var response2 = await _client.GetAsync("/health");

        // Assert
        var traceId1 = response1.Headers.GetValues("X-Trace-Id").FirstOrDefault();
        var traceId2 = response2.Headers.GetValues("X-Trace-Id").FirstOrDefault();

        traceId1.Should().NotBeNull();
        traceId2.Should().NotBeNull();
        traceId1.Should().NotBe(traceId2);
    }

    [Fact]
    public async Task XTraceIdHeader_EqualsTraceParent_WhenProvided()
    {
        // Arrange
        var expectedTraceId = "0123456789abcdef0123456789abcdef";
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("traceparent", $"00-{expectedTraceId}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        var traceId = response.Headers.GetValues("X-Trace-Id").FirstOrDefault();
        traceId.Should().Be(expectedTraceId);
    }

    [Fact]
    public async Task XTraceIdHeader_GeneratedWhenTraceParentInvalid()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("traceparent", "invalid-format");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        var traceId = response.Headers.GetValues("X-Trace-Id").FirstOrDefault();
        traceId.Should().NotBeNull();
        traceId!.Length.Should().Be(32);
        traceId.Should().NotBe("invalid-format");
    }

    [Fact]
    public async Task XTraceIdHeader_PresentOnErrorResponse()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.TryGetValues("X-Trace-Id", out var traceIdValues);
        traceIdValues.Should().NotBeNull();
        var traceId = traceIdValues?.FirstOrDefault() ?? string.Empty;
        traceId.Should().NotBeNullOrEmpty();
    }
}
