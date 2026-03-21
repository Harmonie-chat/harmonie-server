using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.EditMessage;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SendMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class EditConversationMessageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EditConversationMessageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EditConversationMessage_WhenAuthorEditsOwnMessage_ShouldReturnUpdatedMessage()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "original direct", caller.AccessToken);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            new EditMessageRequest("updated direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<EditMessageResponse>();
        payload.Should().NotBeNull();
        payload!.MessageId.Should().Be(message.MessageId);
        payload.ConversationId.Should().Be(conversationId);
        payload.AuthorUserId.Should().Be(caller.UserId);
        payload.Content.Should().Be("updated direct");
        payload.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task EditConversationMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            new EditMessageRequest("updated direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task EditConversationMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "original direct", participantOne.AccessToken);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            new EditMessageRequest("intrusion"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task EditConversationMessage_WhenCallerIsNotAuthor_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "original direct", participantOne.AccessToken);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            new EditMessageRequest("edited by someone else"),
            participantTwo.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
    }

    [Fact]
    public async Task EditConversationMessage_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}",
            new EditMessageRequest("updated direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task EditConversationMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            new EditMessageRequest("updated direct"));

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

    private async Task<HttpResponseMessage> SendAuthorizedPatchAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
