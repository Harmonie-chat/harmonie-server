using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class MessageAttachmentsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public MessageAttachmentsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_WithAttachmentFileIds_ShouldReturnCreatedAndExposeAttachmentsInList()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "attachment-channel");
        var uploadedFile = await UploadAttachmentAsync(author.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();
        sendPayload!.Attachments.Should().ContainSingle();
        sendPayload.Attachments[0].FileId.Should().Be(uploadedFile.FileId);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().ContainSingle();
        listPayload.Items[0].Attachments[0].FileId.Should().Be(uploadedFile.FileId);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenAuthorDeletesOwnAttachment_ShouldReturn204AndRemoveAttachment()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "attachment-delete-channel");
        var uploadedFile = await UploadAttachmentAsync(author.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().BeEmpty();

        var fileResponse = await SendAuthorizedGetAsync($"/api/files/{uploadedFile.FileId}", author.AccessToken);
        fileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenMemberTriesToDeleteAnotherUsersAttachment_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Delete Attachment Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "attachment-auth-channel", guildId: guildId);
        var uploadedFile = await UploadAttachmentAsync(owner.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            owner.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            member.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenAttachmentIsNotOnMessage_ShouldReturn404()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "attachment-notfound-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "message without attachment", author.AccessToken);
        var uploadedFile = await UploadAttachmentAsync(author.AccessToken, "unused.txt", "text/plain", "unused attachment");

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}/attachments/{uploadedFile.FileId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenNotAuthenticated_ShouldReturn401()
    {
        var deleteResponse = await _client.DeleteAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

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

    private async Task<string> CreateGuildAndGetIdAsync(string accessToken, string guildName)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        payload.Should().NotBeNull();

        return payload!.GuildId;
    }

    private async Task InviteMemberAsync(string guildId, string userId, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/members/invite",
            new InviteMemberRequest(userId),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> CreateChannelAndGetIdAsync(
        string accessToken,
        string name,
        string? guildId = null,
        int position = 0)
    {
        if (guildId is null)
        {
            guildId = await CreateGuildAndGetIdAsync(accessToken, $"Guild for {name}");
        }

        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest(name, ChannelTypeInput.Text, position),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();

        return payload!.ChannelId;
    }

    private async Task<string> SendMessageAndGetIdAsync(
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

        return payload!.MessageId;
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
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
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

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
