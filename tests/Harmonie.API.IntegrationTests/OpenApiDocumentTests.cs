using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class OpenApiDocumentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiDocumentTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_ShouldListErrorCodesPerResponseStatus()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        document.Should().NotBeNull();

        var getGuildChannels = document!["paths"]?["/api/guilds/{guildId}/channels"]?["get"]?["responses"];
        getGuildChannels.Should().NotBeNull();

        var badRequestDescription = getGuildChannels!["400"]?["description"]?.GetValue<string>();
        badRequestDescription.Should().NotBeNull();
        badRequestDescription.Should().Contain(ApplicationErrorCodes.Common.ValidationFailed);

        var validationExample = getGuildChannels["400"]?["content"]?["application/json"]?["examples"]?[ApplicationErrorCodes.Common.ValidationFailed]?["value"];
        validationExample.Should().NotBeNull();
        validationExample!["code"]?.GetValue<string>().Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        validationExample["detail"]?.GetValue<string>().Should().Be("Validation failed");
        validationExample["status"]?.GetValue<int>().Should().Be(400);
        validationExample["traceId"]?.GetValue<string>().Should().Be("trace-id");

        var unauthorizedDescription = getGuildChannels["401"]?["description"]?.GetValue<string>();
        unauthorizedDescription.Should().NotBeNull();
        unauthorizedDescription.Should().Contain(ApplicationErrorCodes.Auth.InvalidCredentials);

        var unauthorizedExample = getGuildChannels["401"]?["content"]?["application/json"]?["examples"]?[ApplicationErrorCodes.Auth.InvalidCredentials]?["value"];
        unauthorizedExample.Should().NotBeNull();
        unauthorizedExample!["code"]?.GetValue<string>().Should().Be(ApplicationErrorCodes.Auth.InvalidCredentials);
        unauthorizedExample["detail"]?.GetValue<string>().Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task OpenApiDocument_ShouldListAllRegisterConflictCodesInResponseDescription()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        document.Should().NotBeNull();

        var registerConflict = document!["paths"]?["/api/auth/register"]?["post"]?["responses"]?["409"];
        registerConflict.Should().NotBeNull();

        var description = registerConflict!["description"]?.GetValue<string>();
        description.Should().NotBeNull();
        description.Should().Contain(ApplicationErrorCodes.Auth.DuplicateEmail);
        description.Should().Contain(ApplicationErrorCodes.Auth.DuplicateUsername);

        var conflictExamples = registerConflict["content"]?["application/json"]?["examples"];
        conflictExamples?[ApplicationErrorCodes.Auth.DuplicateEmail]?["value"]?["detail"]?.GetValue<string>()
            .Should().Be("Duplicate email");
        conflictExamples?[ApplicationErrorCodes.Auth.DuplicateUsername]?["value"]?["detail"]?.GetValue<string>()
            .Should().Be("Duplicate username");
    }

    [Fact]
    public async Task OpenApiDocument_ShouldDescribePartialUpdateRequestBodies()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        document.Should().NotBeNull();

        if (document is null)
            throw new InvalidOperationException("The OpenAPI document could not be parsed.");

        var updateMyProfileSchema = ResolveRequestBodySchema(document, "/api/users/me", "patch");
        updateMyProfileSchema.Should().NotBeNull();
        updateMyProfileSchema!["properties"]?["displayName"].Should().NotBeNull();
        updateMyProfileSchema["properties"]?["bio"].Should().NotBeNull();
        updateMyProfileSchema["properties"]?["avatarUrl"].Should().NotBeNull();
        updateMyProfileSchema["properties"]?["avatar"].Should().NotBeNull();
        updateMyProfileSchema["properties"]?["theme"].Should().NotBeNull();
        updateMyProfileSchema["properties"]?["language"].Should().NotBeNull();
        updateMyProfileSchema["properties"]?["displayNameIsSet"].Should().BeNull();
        updateMyProfileSchema["properties"]?["bioIsSet"].Should().BeNull();
        updateMyProfileSchema["properties"]?["avatarUrlIsSet"].Should().BeNull();
        updateMyProfileSchema["properties"]?["avatarIsSet"].Should().BeNull();
        updateMyProfileSchema["properties"]?["themeIsSet"].Should().BeNull();

        var updateChannelSchema = ResolveRequestBodySchema(document, "/api/channels/{channelId}", "patch");
        updateChannelSchema.Should().NotBeNull();
        updateChannelSchema!["properties"]?["name"].Should().NotBeNull();
        updateChannelSchema["properties"]?["position"].Should().NotBeNull();
        updateChannelSchema["properties"]?["nameIsSet"].Should().BeNull();
        updateChannelSchema["properties"]?["positionIsSet"].Should().BeNull();

        var updateMyProfileRequestBody = document["paths"]?["/api/users/me"]?["patch"]?["requestBody"];
        updateMyProfileRequestBody.Should().NotBeNull();
        updateMyProfileRequestBody!["description"]?.GetValue<string>()
            .Should().Contain("Omit a field to keep its current value");
        updateMyProfileRequestBody["content"]?["application/json"]?["example"]?["displayName"]?.GetValue<string>()
            .Should().Be("Alice Harmonie");
        updateMyProfileRequestBody["content"]?["application/json"]?["examples"]?["clearProfileFields"]?["value"]?
            .ToJsonString().Should().Contain("\"bio\":null");

        var updateChannelRequestBody = document["paths"]?["/api/channels/{channelId}"]?["patch"]?["requestBody"];
        updateChannelRequestBody.Should().NotBeNull();
        updateChannelRequestBody!["description"]?.GetValue<string>()
            .Should().Contain("send it as null");
        updateChannelRequestBody["content"]?["application/json"]?["example"]?["name"]?.GetValue<string>()
            .Should().Be("announcements");
    }

    [Fact]
    public async Task OpenApiDocument_ShouldDescribeUploadFileAsMultipartFormData()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        document.Should().NotBeNull();

        var requestBody = document!["paths"]?["/api/uploads"]?["post"]?["requestBody"];
        requestBody.Should().NotBeNull();
        requestBody!["content"]?["multipart/form-data"].Should().NotBeNull();

        var schema = requestBody["content"]?["multipart/form-data"]?["schema"];
        schema.Should().NotBeNull();

        var resolvedSchema = ResolveSchema(document!, schema);
        resolvedSchema.Should().NotBeNull();
        resolvedSchema!["properties"]?["file"].Should().NotBeNull();
    }

    private static JsonNode? ResolveRequestBodySchema(JsonNode document, string path, string method)
    {
        var schema = document["paths"]?[path]?[method]?["requestBody"]?["content"]?["application/json"]?["schema"];
        if (schema is null)
            return null;

        return ResolveSchema(document, schema);
    }

    private static JsonNode? ResolveSchema(JsonNode document, JsonNode? schema)
    {
        if (schema is null)
            return null;

        var reference = schema["$ref"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(reference))
            return schema;

        const string componentsPrefix = "#/components/schemas/";
        if (!reference.StartsWith(componentsPrefix, StringComparison.Ordinal))
            return schema;

        var schemaName = reference[componentsPrefix.Length..];
        return document["components"]?["schemas"]?[schemaName];
    }
}
