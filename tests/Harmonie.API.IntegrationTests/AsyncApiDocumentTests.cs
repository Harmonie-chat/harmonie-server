using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class AsyncApiDocumentTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;

    public AsyncApiDocumentTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAsyncApiJson_ReturnsOkWithApplicationJsonContentType()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/asyncapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetAsyncApiJson_ReturnsParseableDocumentWithAtLeastOneChannel()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/asyncapi/v1.json", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var document = JsonNode.Parse(body);
        document.Should().NotBeNull();
        document!["asyncapi"]?.GetValue<string>().Should().Be("3.1.0");
        document["channels"].Should().NotBeNull();
        document["channels"]!.AsObject().Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAsyncApiJson_DocumentContainsRealtimeChannel()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/asyncapi/v1.json", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = JsonNode.Parse(body);

        document!["channels"]!["Realtime"].Should().NotBeNull();
        document["channels"]!["Realtime"]!["address"]?.GetValue<string>().Should().Be("/hubs/realtime");
    }

    [Fact]
    public async Task GetAsyncApiUi_ReturnsOkWithTextHtmlContentType()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/asyncapi/ui", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

}
