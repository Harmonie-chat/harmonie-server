using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.AcknowledgeRead;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class AcknowledgeReadEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AcknowledgeReadEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcknowledgeRead_WithMessageId_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);
        var message = await SendChannelMessageAsync(channelId, "ack this", owner.AccessToken);

        var response = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(message.MessageId),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WithNullMessageId_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);
        await SendChannelMessageAsync(channelId, "ack all", owner.AccessToken);

        var response = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(null),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenCalledTwice_ShouldBeIdempotent()
    {
        var owner = await RegisterAsync();
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);
        var message = await SendChannelMessageAsync(channelId, "ack twice", owner.AccessToken);

        var firstResponse = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(message.MessageId),
            owner.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(message.MessageId),
            owner.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenChannelHasNoMessages_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);

        var response = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(null),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAckAsync(
            Guid.NewGuid().ToString(),
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);

        var response = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(null),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await RegisterAsync();
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);

        var response = await SendAckAsync(
            channelId,
            new AcknowledgeReadRequest(Guid.NewGuid().ToString()),
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task AcknowledgeRead_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/channels/{Guid.NewGuid()}/ack",
            new AcknowledgeReadRequest(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcknowledgeRead_WithInvalidChannelId_ShouldReturnBadRequest()
    {
        var caller = await RegisterAsync();

        var response = await SendAckAsync(
            "not-a-guid",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task AcknowledgeRead_WithInvalidMessageId_ShouldReturnBadRequest()
    {
        var caller = await RegisterAsync();

        var response = await SendAckAsync(
            Guid.NewGuid().ToString(),
            new AcknowledgeReadRequest("not-a-guid"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    // ─── Helpers ────────────────────────────────────────────────────

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

    private async Task<(string GuildId, string ChannelId)> CreateGuildAndChannelAsync(string accessToken)
    {
        var guildName = $"guild{Guid.NewGuid():N}"[..16];
        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildPayload!.GuildId}/channels",
            new CreateChannelRequest($"chan{Guid.NewGuid():N}"[..16], ChannelTypeInput.Text, 1),
            accessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var channelPayload = await createChannelResponse.Content.ReadFromJsonAsync<CreateChannelResponse>();
        channelPayload.Should().NotBeNull();

        return (guildPayload.GuildId, channelPayload!.ChannelId);
    }

    private async Task<SendMessageResponse> SendChannelMessageAsync(
        string channelId,
        string content,
        string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAckAsync(
        string channelId,
        AcknowledgeReadRequest request,
        string accessToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/channels/{channelId}/ack")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(httpRequest);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
