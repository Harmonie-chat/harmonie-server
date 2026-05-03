using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ConversationSendMessageRequest = Harmonie.Application.Features.Conversations.SendMessage.SendMessageRequest;
using ConversationSendMessageResponse = Harmonie.Application.Features.Conversations.SendMessage.SendMessageResponse;

namespace Harmonie.API.IntegrationTests;

public sealed class PinMessageEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PinMessageEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Channel pin tests ──────────────────────────────────────────

    [Fact]
    public async Task PinChannelMessage_WhenCallerIsMember_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "pin this", owner.AccessToken);

        var response = await SendAuthorizedPutAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PinChannelMessage_WhenCalledTwice_ShouldBeIdempotent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "pin me twice", owner.AccessToken);

        var firstResponse = await SendAuthorizedPutAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            owner.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await SendAuthorizedPutAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            owner.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PinChannelMessage_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await SendAuthorizedPutAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/pin",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task PinChannelMessage_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "can't pin", owner.AccessToken);

        var response = await SendAuthorizedPutAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task PinChannelMessage_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await SendAuthorizedPutAsync(
            $"/api/channels/{channelId}/messages/{Guid.NewGuid()}/pin",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Pin.MessageNotFound);
    }

    [Fact]
    public async Task PinChannelMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/pin",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Conversation pin tests ─────────────────────────────────────

    [Fact]
    public async Task PinConversationMessage_WhenCallerIsParticipant_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "pin this dm", caller.AccessToken);

        var response = await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PinConversationMessage_WhenCalledTwice_ShouldBeIdempotent()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "pin twice dm", caller.AccessToken);

        var firstResponse = await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            caller.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            caller.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PinConversationMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await SendAuthorizedPutAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/pin",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task PinConversationMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "private dm", participantOne.AccessToken);

        var response = await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task PinConversationMessage_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var response = await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}/pin",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Pin.MessageNotFound);
    }

    [Fact]
    public async Task PinConversationMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/pin",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private async Task<ConversationSendMessageResponse> SendConversationMessageAsync(
        Guid conversationId,
        string content,
        string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new ConversationSendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<ConversationSendMessageResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
