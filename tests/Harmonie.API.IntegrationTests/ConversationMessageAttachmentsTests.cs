using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.GetMessages;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SendMessage;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ConversationMessageAttachmentsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ConversationMessageAttachmentsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WhenAuthorDeletesOwnAttachment_ShouldReturn204AndRemoveAttachment()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var uploadedFile = await UploadAttachmentAsync(caller.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            caller.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().BeEmpty();

        var fileResponse = await SendAuthorizedGetAsync($"/api/files/{uploadedFile.FileId}", caller.AccessToken);
        fileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var uploadedFile = await UploadAttachmentAsync(participantOne.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            participantOne.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WhenCallerIsNotAuthor_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var uploadedFile = await UploadAttachmentAsync(participantOne.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            participantOne.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            participantTwo.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WhenAttachmentIsNotOnMessage_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "message without attachment", caller.AccessToken);
        var uploadedFile = await UploadAttachmentAsync(caller.AccessToken, "unused.txt", "text/plain", "unused attachment");

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/attachments/{uploadedFile.FileId}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}");

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

    private async Task<UploadFileResponse> UploadAttachmentAsync(
        string accessToken,
        string fileName,
        string contentType,
        string content)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/files/uploads")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
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
