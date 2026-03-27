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

public sealed class AddReactionEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AddReactionEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Channel reaction tests ─────────────────────────────────────

    [Fact]
    public async Task AddChannelReaction_WhenCallerIsMember_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "react to this", owner.AccessToken);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddChannelReaction_WhenCalledTwice_ShouldBeIdempotent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "react twice", owner.AccessToken);

        var firstResponse = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddChannelReaction_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%F0%9F%91%8D",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task AddChannelReaction_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "can't react", owner.AccessToken);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task AddChannelReaction_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{Guid.NewGuid()}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task AddChannelReaction_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%F0%9F%91%8D",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Conversation reaction tests ────────────────────────────────

    [Fact]
    public async Task AddConversationReaction_WhenCallerIsParticipant_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "react to this dm", caller.AccessToken);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddConversationReaction_WhenCalledTwice_ShouldBeIdempotent()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "react twice dm", caller.AccessToken);

        var firstResponse = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddConversationReaction_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task AddConversationReaction_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "private dm", participantOne.AccessToken);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task AddConversationReaction_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task AddConversationReaction_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%E2%9D%A4",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

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

        var payload = await response.Content.ReadFromJsonAsync<ConversationSendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutNoBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
