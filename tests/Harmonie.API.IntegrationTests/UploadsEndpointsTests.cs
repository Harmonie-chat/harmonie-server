using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UploadsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UploadsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadFile_WithValidMultipartRequest_ShouldReturnCreated()
    {
        var user = await RegisterAsync();
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

        var response = await SendAuthorizedMultipartAsync(client, "/api/uploads", content, user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        payload.Should().NotBeNull();
        payload!.Filename.Should().Be("avatar.png");
        payload.ContentType.Should().Be("image/png");
        payload.SizeBytes.Should().Be(4);
        payload.Url.Should().Contain("/uploads/");
        fakeStorage.UploadedObjects.Should().ContainSingle();
    }

    [Fact]
    public async Task UploadFile_WithUnsupportedContentType_ShouldReturnBadRequest()
    {
        var user = await RegisterAsync();
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

        var response = await SendAuthorizedMultipartAsync(client, "/api/uploads", content, user.AccessToken);

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

        var response = await _client.PostAsync("/api/uploads", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
