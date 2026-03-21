using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.GetMessages;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SendMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class DeleteConversationMessageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DeleteConversationMessageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteConversationMessage_WhenAuthorDeletesOwnMessage_ShouldReturn204()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "delete me", caller.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteConversationMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task DeleteConversationMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "delete me", participantOne.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task DeleteConversationMessage_WhenCallerIsNotAuthor_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "delete me", participantOne.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            participantTwo.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteConversationMessage_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteConversationMessage_WhenDeleted_ShouldBeExcludedFromReadEndpoint()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var visibleMessage = await SendConversationMessageAsync(conversationId, "keep me", target.AccessToken);
        await Task.Delay(20);
        var deletedMessage = await SendConversationMessageAsync(conversationId, "delete me", caller.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{deletedMessage.MessageId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].MessageId.Should().Be(visibleMessage.MessageId);
    }

    [Fact]
    public async Task DeleteConversationMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> OpenConversationAsync(string accessToken, string targetUserId)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
    }

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

    private async Task<SendMessageResponse> SendConversationMessageAsync(
        string conversationId,
        string content,
        string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
