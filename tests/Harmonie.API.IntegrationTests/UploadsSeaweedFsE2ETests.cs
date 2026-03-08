using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.Runtime;
using Amazon.S3;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UploadsSeaweedFsE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Endpoint =
        Environment.GetEnvironmentVariable("WEED_TEST_ENDPOINT") ?? "http://127.0.0.1:4900";

    private const string BucketName = "harmonie-uploads";
    private const string Region = "us-east-1";
    private const string AccessKeyId = "harmonie-key";
    private const string SecretAccessKey = "harmonie-secret-key-for-dev";

    private static string PublicBaseUrl => $"{Endpoint}/{BucketName}";

    private readonly WebApplicationFactory<Program> _factory;

    public UploadsSeaweedFsE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadFile_WithRealSeaweedFsBackend_ShouldStoreObject()
    {
        await EnsureAvailableAsync();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ObjectStorage:Provider"] = "s3",
                    ["ObjectStorage:Endpoint"] = Endpoint,
                    ["ObjectStorage:PublicBaseUrl"] = PublicBaseUrl,
                    ["ObjectStorage:BucketName"] = BucketName,
                    ["ObjectStorage:Region"] = Region,
                    ["ObjectStorage:AccessKeyId"] = AccessKeyId,
                    ["ObjectStorage:SecretAccessKey"] = SecretAccessKey,
                    ["ObjectStorage:ForcePathStyle"] = bool.TrueString,
                    ["ObjectStorage:CreateBucketIfMissing"] = bool.TrueString
                });
            });
        });

        using var client = factory.CreateClient();
        var user = await RegisterAsync(client);

        using var multipart = CreateMultipartContent("seaweedfs.txt", "text/plain", "hello from seaweedfs");
        var response = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Fail(
                $"Expected {(int)HttpStatusCode.Created} Created but received {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{responseBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);
        Assert.Equal("seaweedfs.txt", payload!.Filename);
        Assert.Equal("text/plain", payload.ContentType);

        var objectKey = payload.Url[(PublicBaseUrl.TrimEnd('/').Length + 1)..];
        using var s3Client = CreateS3Client();
        using var objectResponse = await s3Client.GetObjectAsync(BucketName, objectKey);
        using var reader = new StreamReader(objectResponse.ResponseStream);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("hello from seaweedfs", content);
    }

    private static async Task EnsureAvailableAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            await http.GetAsync(Endpoint);
        }
        catch
        {
            throw new InvalidOperationException(
                $"SeaweedFS is not reachable at {Endpoint}. " +
                $"Start it with: podman compose up -d seaweedfs");
        }
    }

    private static AmazonS3Client CreateS3Client()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = Region,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        return new AmazonS3Client(new BasicAWSCredentials(AccessKeyId, SecretAccessKey), config);
    }

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
}
