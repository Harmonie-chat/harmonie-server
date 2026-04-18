using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.AcknowledgeRead;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class AcknowledgeReadEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AcknowledgeReadEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcknowledgeRead_WithMessageId_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "ack this", owner.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(message.MessageId),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WithNullMessageId_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "ack all", owner.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(null),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenCalledTwice_ShouldBeIdempotent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "ack twice", owner.AccessToken);

        var firstResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(message.MessageId),
            owner.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(message.MessageId),
            owner.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenChannelHasNoMessages_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(null),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{Guid.NewGuid()}/ack",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(null),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/ack",
            new AcknowledgeReadRequest(Guid.NewGuid()),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task AcknowledgeRead_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/channels/{Guid.NewGuid()}/ack",
            new AcknowledgeReadRequest(null),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcknowledgeRead_WithInvalidChannelId_ShouldReturnBadRequest()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/channels/not-a-guid/ack",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task AcknowledgeRead_WithInvalidMessageId_ShouldReturnBadRequest()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{Guid.NewGuid()}/ack",
            new { messageId = "not-a-guid" },
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

}
