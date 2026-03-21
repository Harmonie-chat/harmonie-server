using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Application.Interfaces.Uploads;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UploadsEndpointsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UploadsEndpointsTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadFile_WithValidMultipartRequest_ShouldReturnCreated()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var fakeStorage = new FakeObjectStorageService();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IObjectStorageService>();
                services.AddSingleton<IObjectStorageService>(fakeStorage);
            });
        });

        using var client = factory.CreateClient();
        using var content = CreateMultipartContent("avatar.png", "image/png", [1, 2, 3, 4]);

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", content, user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        payload.Should().NotBeNull();
        payload!.Filename.Should().Be("avatar.png");
        payload.ContentType.Should().Be("image/png");
        payload.SizeBytes.Should().Be(4);
        Guid.TryParse(payload.FileId, out _).Should().BeTrue();
        fakeStorage.UploadedObjects.Should().ContainSingle();
    }

    [Fact]
    public async Task UploadFile_WithUnsupportedContentType_ShouldReturnBadRequest()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var fakeStorage = new FakeObjectStorageService();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IObjectStorageService>();
                services.AddSingleton<IObjectStorageService>(fakeStorage);
            });
        });

        using var client = factory.CreateClient();
        using var content = CreateMultipartContent("malware.exe", "application/octet-stream", [1, 2, 3, 4]);

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", content, user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        fakeStorage.UploadedObjects.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFile_WithGuildIconPurpose_ShouldReturnCreated()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var fakeStorage = new FakeObjectStorageService();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IObjectStorageService>();
                services.AddSingleton<IObjectStorageService>(fakeStorage);
            });
        });

        using var client = factory.CreateClient();
        using var content = CreateMultipartContent("icon.png", "image/png", [1, 2, 3, 4]);
        content.Add(new StringContent("guildIcon"), "purpose");

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", content, user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        payload.Should().NotBeNull();
        payload!.Filename.Should().Be("icon.png");
        fakeStorage.UploadedObjects.Should().ContainSingle();
    }

    [Fact]
    public async Task UploadFile_WithInvalidPurpose_ShouldReturnBadRequest()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var fakeStorage = new FakeObjectStorageService();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IObjectStorageService>();
                services.AddSingleton<IObjectStorageService>(fakeStorage);
            });
        });

        using var client = factory.CreateClient();
        using var content = CreateMultipartContent("file.png", "image/png", [1, 2, 3, 4]);
        content.Add(new StringContent("invalid_purpose"), "purpose");

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", content, user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        fakeStorage.UploadedObjects.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFile_WithAvatarPurpose_ShouldReturnBadRequest()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var fakeStorage = new FakeObjectStorageService();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IObjectStorageService>();
                services.AddSingleton<IObjectStorageService>(fakeStorage);
            });
        });

        using var client = factory.CreateClient();
        using var content = CreateMultipartContent("avatar.png", "image/png", [1, 2, 3, 4]);
        content.Add(new StringContent("avatar"), "purpose");

        var response = await SendAuthorizedMultipartAsync(client, "/api/files/uploads", content, user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        fakeStorage.UploadedObjects.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFile_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        using var content = CreateMultipartContent("avatar.png", "image/png", [1, 2, 3, 4]);

        var response = await _client.PostAsync("/api/files/uploads", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static MultipartFormDataContent CreateMultipartContent(
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedMultipartAsync(
        HttpClient client,
        string uri,
        MultipartFormDataContent content,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private sealed class FakeObjectStorageService : IObjectStorageService
    {
        public ConcurrentDictionary<string, byte[]> UploadedObjects { get; } = new();

        public string BuildPublicUrl(string storageKey)
            => $"https://files.test/{storageKey}";

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            UploadedObjects.TryRemove(storageKey, out _);
            return Task.CompletedTask;
        }

        public Task<Stream?> GetStreamAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (UploadedObjects.TryGetValue(storageKey, out var bytes))
                return Task.FromResult<Stream?>(new MemoryStream(bytes));

            return Task.FromResult<Stream?>(null);
        }

        public async Task<ObjectStorageUploadResult> UploadAsync(
            ObjectStorageUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream();
            await request.Content.CopyToAsync(memoryStream, cancellationToken);
            UploadedObjects[request.StorageKey] = memoryStream.ToArray();
            return ObjectStorageUploadResult.Succeeded();
        }
    }
}
