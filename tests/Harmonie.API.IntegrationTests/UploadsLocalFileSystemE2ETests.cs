using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UploadsLocalFileSystemE2ETests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempDir;

    public UploadsLocalFileSystemE2ETests(WebApplicationFactory<Program> factory)
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

        var user = await RegisterAsync(client);
        using var multipart = CreateMultipartContent("hello.txt", "text/plain", "hello from filesystem");

        var response = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);
        Assert.Equal("hello.txt", payload!.Filename);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.StartsWith("/api/files/", payload.Url);

        // Verify file exists on disk by downloading through the authorized endpoint
        var downloadResponse = await SendAuthorizedGetAsync(client, payload.Url, user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        var diskContent = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("hello from filesystem", diskContent);
    }

    [Fact]
    public async Task DownloadFile_WithLocalFileSystemProvider_ShouldServeFileViaAuthenticatedEndpoint()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await RegisterAsync(client);
        using var multipart = CreateMultipartContent("image.png", "image/png", "fake-png-content");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var payload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);

        var fileResponse = await SendAuthorizedGetAsync(client, payload!.Url, user.AccessToken);

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

        var user = await RegisterAsync(client);
        using var multipart = CreateMultipartContent("secret.txt", "text/plain", "secret content");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var payload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);

        var fileResponse = await client.GetAsync(payload!.Url);

        Assert.Equal(HttpStatusCode.Unauthorized, fileResponse.StatusCode);
    }

    [Fact]
    public async Task UploadFile_WithLocalFileSystemProvider_ShouldReturnCorrectMetadata()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await RegisterAsync(client);
        var bytes = System.Text.Encoding.UTF8.GetBytes("test content");
        using var multipart = CreateMultipartContent("doc.txt", "text/plain", "test content");

        var response = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);
        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.FileId));
        Assert.Equal("doc.txt", payload.Filename);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.Equal(bytes.Length, payload.SizeBytes);
        Assert.StartsWith("/api/files/", payload.Url);
    }

    [Fact]
    public async Task UpdateGuild_WhenReplacingUnsharedLocalIconUrl_ShouldDeleteOldStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await RegisterAsync(client);
        using var multipart = CreateMultipartContent("guild-icon.txt", "text/plain", "guild icon");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var createGuildResponse = await SendAuthorizedPostAsync(
            client,
            "/api/guilds",
            new CreateGuildRequest("Guild With Uploaded Icon"),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildResponse.StatusCode);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(createGuildPayload);

        var setIconResponse = await SendAuthorizedPatchAsync(
            client,
            $"/api/guilds/{createGuildPayload!.GuildId}",
            new { iconUrl = uploadPayload!.Url, icon = new { color = "#F59E0B" } },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setIconResponse.StatusCode);

        var clearIconResponse = await SendAuthorizedPatchAsync(
            client,
            $"/api/guilds/{createGuildPayload.GuildId}",
            new { iconUrl = (string?)null },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, clearIconResponse.StatusCode);

        var oldFileResponse = await SendAuthorizedGetAsync(client, uploadPayload.Url, user.AccessToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, oldFileResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateGuild_WhenLocalIconUrlIsStillShared_ShouldKeepStoredFile()
    {
        using var factory = BuildFactory();
        using var client = factory.CreateClient();

        var user = await RegisterAsync(client);
        using var multipart = CreateMultipartContent("shared-guild-icon.txt", "text/plain", "shared guild icon");

        var uploadResponse = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(uploadPayload);

        var createGuildOneResponse = await SendAuthorizedPostAsync(
            client,
            "/api/guilds",
            new CreateGuildRequest("Guild One"),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildOneResponse.StatusCode);

        var createGuildTwoResponse = await SendAuthorizedPostAsync(
            client,
            "/api/guilds",
            new CreateGuildRequest("Guild Two"),
            user.AccessToken);
        Assert.Equal(HttpStatusCode.Created, createGuildTwoResponse.StatusCode);

        var guildOne = await createGuildOneResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        var guildTwo = await createGuildTwoResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        Assert.NotNull(guildOne);
        Assert.NotNull(guildTwo);

        var setGuildOneIconResponse = await SendAuthorizedPatchAsync(
            client,
            $"/api/guilds/{guildOne!.GuildId}",
            new { iconUrl = uploadPayload!.Url },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setGuildOneIconResponse.StatusCode);

        var setGuildTwoIconResponse = await SendAuthorizedPatchAsync(
            client,
            $"/api/guilds/{guildTwo!.GuildId}",
            new { iconUrl = uploadPayload.Url },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, setGuildTwoIconResponse.StatusCode);

        var clearGuildOneIconResponse = await SendAuthorizedPatchAsync(
            client,
            $"/api/guilds/{guildOne.GuildId}",
            new { iconUrl = (string?)null },
            user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, clearGuildOneIconResponse.StatusCode);

        var sharedFileResponse = await SendAuthorizedGetAsync(client, uploadPayload.Url, user.AccessToken);
        Assert.Equal(HttpStatusCode.OK, sharedFileResponse.StatusCode);

        var sharedFileContent = await sharedFileResponse.Content.ReadAsStringAsync();
        Assert.Equal("shared guild icon", sharedFileContent);
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

    private static async Task<RegisterResponse> RegisterAsync(HttpClient client)
    {
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid():N}@harmonie.chat",
            Username: $"user{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(payload);
        return payload!;
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

    private static async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        HttpClient client,
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedPostAsync(
        HttpClient client,
        string uri,
        object payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedPatchAsync(
        HttpClient client,
        string uri,
        object payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }
}
