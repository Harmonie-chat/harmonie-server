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

public sealed class SendConversationMessageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SendConversationMessageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendConversationMessage_WhenCallerIsParticipant_ShouldCreateMessage()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("hello direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.AuthorUserId.Should().Be(caller.UserId);
        payload.Content.Should().Be("hello direct");
    }

    [Fact]
    public async Task SendConversationMessage_WithAttachmentFileIds_ShouldCreateMessageAndExposeAttachmentsInList()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var uploadedFile = await UploadAttachmentAsync(caller.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("hello direct", [uploadedFile.FileId]),
            caller.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();
        sendPayload!.Attachments.Should().ContainSingle();
        sendPayload.Attachments[0].FileId.Should().Be(uploadedFile.FileId);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().ContainSingle();
        listPayload.Items[0].Attachments[0].FileId.Should().Be(uploadedFile.FileId);
    }

    [Fact]
    public async Task SendConversationMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new SendMessageRequest("hello direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task SendConversationMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);

        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("intrusion"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task SendConversationMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new SendMessageRequest("hello direct"));

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
}
