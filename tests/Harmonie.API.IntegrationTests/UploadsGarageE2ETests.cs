using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UploadsGarageE2ETests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public UploadsGarageE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadFile_WithRealGarageBackend_ShouldStoreObject()
    {
        await PodmanGarageEnvironment.InitializeAsync();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ObjectStorage:Endpoint"] = PodmanGarageEnvironment.Endpoint,
                    ["ObjectStorage:PublicBaseUrl"] = PodmanGarageEnvironment.PublicBaseUrl,
                    ["ObjectStorage:BucketName"] = PodmanGarageEnvironment.BucketName,
                    ["ObjectStorage:Region"] = PodmanGarageEnvironment.Region,
                    ["ObjectStorage:AccessKeyId"] = PodmanGarageEnvironment.AccessKeyId,
                    ["ObjectStorage:SecretAccessKey"] = PodmanGarageEnvironment.SecretAccessKey,
                    ["ObjectStorage:ForcePathStyle"] = bool.TrueString,
                    ["ObjectStorage:CreateBucketIfMissing"] = bool.TrueString
                });
            });
        });

        using var client = factory.CreateClient();
        var user = await RegisterAsync(client);

        using var multipart = CreateMultipartContent("garage.txt", "text/plain", "hello from garage");
        var response = await SendAuthorizedMultipartAsync(client, "/api/uploads", multipart, user.AccessToken);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Fail(
                $"Expected {(int)HttpStatusCode.Created} Created but received {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{responseBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        Assert.NotNull(payload);
        Assert.Equal("garage.txt", payload!.Filename);
        Assert.Equal("text/plain", payload.ContentType);

        var objectKey = payload.Url[(PodmanGarageEnvironment.PublicBaseUrl.TrimEnd('/').Length + 1)..];
        using var s3Client = PodmanGarageEnvironment.CreateS3Client();
        using var objectResponse = await s3Client.GetObjectAsync(
            PodmanGarageEnvironment.BucketName,
            objectKey);
        using var reader = new StreamReader(objectResponse.ResponseStream);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("hello from garage", content);
    }

    public Task InitializeAsync()
        => Task.CompletedTask;

    public Task DisposeAsync()
        => PodmanGarageEnvironment.DisposeAsync();

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
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static class PodmanGarageEnvironment
    {
        private static readonly SemaphoreSlim Semaphore = new(1, 1);
        private static bool _initialized;

        public const string AccessKeyId = "GK0123456789abcdef01234567";
        public const string SecretAccessKey = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        public const string BucketName = "harmonie-uploads";
        public const string Region = "garage";
        private static readonly string RepositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        private static readonly string ComposeFile = Path.Combine(RepositoryRoot, "docker-compose.yml");
        private static readonly string ProjectName =
            Environment.GetEnvironmentVariable("GARAGE_TEST_COMPOSE_PROJECT") ?? "harmonie-upload-e2e";
        private static readonly bool ManagedExternally = string.Equals(
            Environment.GetEnvironmentVariable("GARAGE_TEST_MANAGED_EXTERNALLY"),
            "1",
            StringComparison.Ordinal);
        private static readonly int S3Port = GetPort("GARAGE_TEST_S3_PORT", 4900);
        private static readonly int RpcPort = GetPort("GARAGE_TEST_RPC_PORT", 4901);
        private static readonly int AdminPort = GetPort("GARAGE_TEST_ADMIN_PORT", 4903);

        public static string Endpoint => $"http://127.0.0.1:{S3Port}";

        public static string PublicBaseUrl => $"{Endpoint}/{BucketName}";

        public static async Task InitializeAsync()
        {
            if (_initialized)
                return;

            await Semaphore.WaitAsync();
            try
            {
                if (_initialized)
                    return;

                if (!ManagedExternally)
                {
                    await RunPodmanComposeAsync("down -v", throwOnError: false);
                    await RunPodmanComposeAsync("up -d garage");
                }

                var statusOutput = await WaitForStatusAsync();
                var nodeId = ExtractNodeId(statusOutput);

                await RunGarageCommandAsync($"layout assign {nodeId} -z local -c 10GB");
                await RunGarageCommandAsync("layout apply --version 1");
                await RunGarageCommandAsync(
                    $"key import --yes -n harmonie-uploads {AccessKeyId} {SecretAccessKey}");
                await RunGarageCommandAsync("key allow --create-bucket harmonie-uploads");
                await RunGarageCommandAsync("bucket create harmonie-uploads");
                await RunGarageCommandAsync(
                    "bucket allow harmonie-uploads --read --write --owner --key harmonie-uploads");

                _initialized = true;
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public static async Task DisposeAsync()
        {
            if (ManagedExternally)
                return;

            await Semaphore.WaitAsync();
            try
            {
                if (!_initialized)
                    return;

                await RunPodmanComposeAsync("down -v", throwOnError: false);
                _initialized = false;
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public static AmazonS3Client CreateS3Client()
        {
            var config = new AmazonS3Config
            {
                ServiceURL = Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            return new AmazonS3Client(
                new BasicAWSCredentials(AccessKeyId, SecretAccessKey),
                config);
        }

        private static async Task<string> WaitForStatusAsync()
        {
            Exception? lastException = null;

            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    return await RunGarageCommandAsync("status");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(500);
                }
            }

            throw new InvalidOperationException("Garage did not become ready in time.", lastException);
        }

        private static string ExtractNodeId(string statusOutput)
        {
            var match = Regex.Match(statusOutput, @"\b[0-9a-f]{16}\b", RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new InvalidOperationException($"Could not extract Garage node ID from status output:{Environment.NewLine}{statusOutput}");

            return match.Value;
        }

        private static int GetPort(string variableName, int fallbackValue)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            return int.TryParse(value, out var parsedValue) ? parsedValue : fallbackValue;
        }

        private static Task<string> RunGarageCommandAsync(string arguments)
            => RunPodmanComposeAsync($"exec -T garage /garage -c /etc/garage.toml {arguments}");

        private static async Task<string> RunPodmanComposeAsync(string arguments, bool throwOnError = true)
        {
            var startInfo = new ProcessStartInfo("podman", $"compose -f {ComposeFile} -p {ProjectName} {arguments}")
            {
                WorkingDirectory = RepositoryRoot
            };

            startInfo.Environment["GARAGE_S3_PORT"] = S3Port.ToString();
            startInfo.Environment["GARAGE_RPC_PORT"] = RpcPort.ToString();
            startInfo.Environment["GARAGE_ADMIN_PORT"] = AdminPort.ToString();

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && throwOnError)
            {
                throw new InvalidOperationException(
                    $"podman compose {arguments} failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{stderr}");
            }

            return string.Concat(stdout, stderr);
        }
    }
}
