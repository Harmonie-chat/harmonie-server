using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
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
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "original direct", caller.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
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
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPatchAsync(
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
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "original direct", participantOne.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
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
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "original direct", participantOne.AccessToken);

        var response = await _client.SendAuthorizedPatchAsync(
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
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedPatchAsync(
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
        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
    }

    private async Task<SendMessageResponse> SendConversationMessageAsync(
        string conversationId,
        string content,
        string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }
}
