using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Conversations.OpenConversation;
using ConversationSendMessageRequest = Harmonie.Application.Features.Conversations.SendMessage.SendMessageRequest;
using ConversationSendMessageResponse = Harmonie.Application.Features.Conversations.SendMessage.SendMessageResponse;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UploadsLocalFileSystemE2ETests : IClassFixture<HarmonieWebApplicationFactory>, IDisposable
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly string _tempDir;

    public UploadsLocalFileSystemE2ETests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _tempDir = Path.Combine(Path.GetTempPath(), $"harmonie-uploads-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task UploadFile_WithLocalFileSystemProvider_ShouldWriteFileToDisk()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("hello.txt", "text/plain", "hello from filesystem");

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);
        Assert.Equal("hello.txt", payload!.Filename);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.False(string.IsNullOrWhiteSpace(payload.FileId));

        // Verify file exists on disk by downloading through the authorized endpoint
        var downloadResponse = await client.SendAuthorizedGetAsync($"/api/files/{payload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        var diskContent = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("hello from filesystem", diskContent);
    }

    [Fact]
    public async Task DownloadFile_WithLocalFileSystemProvider_ShouldServeFileViaAuthenticatedEndpoint()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("image.png", "image/png", "fake-png-content");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var payload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);

        var fileResponse = await client.SendAuthorizedGetAsync($"/api/files/{payload!.FileId}", user.AccessToken);

        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        Assert.Equal("image/png", fileResponse.Content.Headers.ContentType?.MediaType);

        var fileContent = await fileResponse.Content.ReadAsStringAsync();
        Assert.Equal("fake-png-content", fileContent);
    }

    [Fact]
    public async Task DownloadFile_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("secret.txt", "text/plain", "secret content");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var payload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);

        var fileResponse = await client.GetAsync($"/api/files/{payload!.FileId}");

        Assert.Equal(HttpStatusCode.Unauthorized, fileResponse.StatusCode);
    }

    [Fact]
    public async Task UploadFile_WithLocalFileSystemProvider_ShouldReturnCorrectMetadata()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        var bytes = System.Text.Encoding.UTF8.GetBytes("test content");
        using var multipart = CreateMultipartContent("doc.txt", "text/plain", "test content");

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.FileId));
        Assert.Equal("doc.txt", payload.Filename);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.Equal(bytes.Length, payload.SizeBytes);
    }

    [Fact]
    public async Task UpdateGuild_WhenClearingIconFile_ShouldDeleteOldStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("guild-icon.txt", "text/plain", "guild icon");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var createGuildResponse = await client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild With Uploaded Icon"),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildResponse.StatusCode);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(createGuildPayload);

        var setIconResponse = await client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}",
            new { iconFileId = uploadPayload!.FileId, icon = new { color = "#F59E0B" } },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setIconResponse.StatusCode);

        var clearIconResponse = await client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new { iconFileId = (string?)null },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, clearIconResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{uploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateGuild_WhenReplacingIconFile_ShouldDeletePreviousFileAndKeepNewOne()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var initialMultipart = CreateMultipartContent("guild-icon-initial.txt", "text/plain", "initial guild icon");
        using var replacementMultipart = CreateMultipartContent("guild-icon-replacement.txt", "text/plain", "replacement guild icon");

        var initialUploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", initialMultipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, initialUploadResponse.StatusCode);

        var replacementUploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", replacementMultipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, replacementUploadResponse.StatusCode);

        var initialUploadPayload = await initialUploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        var replacementUploadPayload = await replacementUploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(initialUploadPayload);
        Assert.NotNull(replacementUploadPayload);

        var createGuildResponse = await client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Replace"),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildResponse.StatusCode);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(guild);

        var setInitialIconResponse = await client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guild!.GuildId}",
            new { iconFileId = initialUploadPayload!.FileId },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setInitialIconResponse.StatusCode);

        var replaceIconResponse = await client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guild.GuildId}",
            new { iconFileId = replacementUploadPayload!.FileId },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, replaceIconResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{initialUploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);

        var newFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{replacementUploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, newFileResponse.StatusCode);

        var newFileContent = await newFileResponse.Content.ReadAsStringAsync();
        Assert.Equal("replacement guild icon", newFileContent);
    }

    [Fact]
    public async Task DeleteGuild_WhenGuildHasUploadedIcon_ShouldDeleteStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("guild-icon-delete.txt", "text/plain", "guild icon to delete");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var createGuildResponse = await client.SendAuthorizedPostAsync(
            "/api/guilds",
            new
            {
                name = "Guild Delete Icon",
                iconFileId = uploadPayload!.FileId
            },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildResponse.StatusCode);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(createGuildPayload);

        var deleteGuildResponse = await client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}",
            user.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteGuildResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{uploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMyAvatar_WhenUserHasUploadedAvatar_ShouldDeleteStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("avatar-delete.txt", "text/plain", "avatar to delete");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var setAvatarResponse = await client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { avatarFileId = uploadPayload!.FileId },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setAvatarResponse.StatusCode);

        var deleteAvatarResponse = await client.SendAuthorizedDeleteAsync(
            "/api/users/me/avatar",
            user.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteAvatarResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{uploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenMessageHasUploadedAttachment_ShouldDeleteStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        var guildId = await CreateGuildAsync(client, user.AccessToken, "Attachment Delete Guild");
        var channelId = await CreateChannelAsync(client, user.AccessToken, guildId, "attachment-delete-channel");
        using var multipart = CreateMultipartContent("attachment-delete.txt", "text/plain", "attachment to delete");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var sendMessageResponse = await client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadPayload!.FileId]),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, sendMessageResponse.StatusCode);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        Assert.NotNull(sendMessagePayload);

        var deleteAttachmentResponse = await client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendMessagePayload!.MessageId}/attachments/{uploadPayload.FileId}",
            user.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteAttachmentResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{uploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteConversationMessageAttachment_WhenMessageHasUploadedAttachment_ShouldDeleteStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var caller = await AuthTestHelper.RegisterAsync(client);
        var target = await AuthTestHelper.RegisterAsync(client);
        var conversationId = await OpenConversationAsync(client, caller.AccessToken, target.UserId);
        using var multipart = CreateMultipartContent("conversation-attachment-delete.txt", "text/plain", "conversation attachment to delete");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, caller.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var sendMessageResponse = await client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new ConversationSendMessageRequest("message with attachment", [uploadPayload!.FileId]),
            caller.AccessToken);
        Assert.Equal(HttpStatusCode.Created, sendMessageResponse.StatusCode);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<ConversationSendMessageResponse>();
        Assert.NotNull(sendMessagePayload);

        var deleteAttachmentResponse = await client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{sendMessagePayload!.MessageId}/attachments/{uploadPayload.FileId}",
            caller.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteAttachmentResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{uploadPayload.FileId}", caller.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteGuildIcon_WhenGuildHasUploadedIcon_ShouldDeleteStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await AuthTestHelper.RegisterAsync(client);
        using var multipart = CreateMultipartContent("guild-icon-endpoint-delete.txt", "text/plain", "guild icon endpoint delete");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var createGuildResponse = await client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Guild Icon Endpoint Delete"),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildResponse.StatusCode);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(guild);

        var setIconResponse = await client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guild!.GuildId}",
            new { iconFileId = uploadPayload!.FileId },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setIconResponse.StatusCode);

        var deleteIconResponse = await client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{guild.GuildId}/icon",
            user.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteIconResponse.StatusCode);

        var oldFileResponse = await client.SendAuthorizedGetAsync($"/api/files/{uploadPayload.FileId}", user.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, oldFileResponse.StatusCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private WebApplicationFactory<Program> BuildFactory()
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ObjectStorage:LocalBasePath"] = _tempDir,
                    ["ObjectStorage:LocalBaseUrl"] = "http://localhost/files"
                });
            });
        });

    private static async Task<string> OpenConversationAsync(
        HttpClient client,
        string accessToken,
        string targetUserId)
    {
        var response = await client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        Assert.NotNull(payload);
        return payload!.ConversationId;
    }

    private static async Task<string> CreateGuildAsync(
        HttpClient client,
        string accessToken,
        string name)
    {
        var response = await client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(name),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(payload);
        return payload!.GuildId;
    }

    private static async Task<string> CreateChannelAsync(
        HttpClient client,
        string accessToken,
        string guildId,
        string name)
    {
        var response = await client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new { name, type = "Text", position = 0 },
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        Assert.NotNull(payload);
        return payload!.ChannelId;
    }

    private static MultipartFormDataContent CreateMultipartContent(
        string fileName,
        string contentType,
        string content)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedMultipartAsync(
        HttpClient client,
        string uri,
        MultipartFormDataContent content,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }
}
