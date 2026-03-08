using FluentAssertions;
using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure.Configuration;
using Harmonie.Infrastructure.ObjectStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Infrastructure.Tests;

public sealed class LocalFileSystemObjectStorageServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public LocalFileSystemObjectStorageServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"harmonie-local-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task UploadAsync_WithNestedStorageKey_ShouldWriteInsideConfiguredBasePath()
    {
        var service = CreateService();
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello"));

        var result = await service.UploadAsync(
            new ObjectStorageUploadRequest(
                "uploads/2026/03/test.txt",
                "text/plain",
                content.Length,
                content));

        result.Success.Should().BeTrue();
        File.ReadAllText(Path.Combine(_tempDirectory, "uploads", "2026", "03", "test.txt"))
            .Should().Be("hello");
    }

    [Fact]
    public async Task UploadAsync_WithEscapingStorageKey_ShouldFail()
    {
        var service = CreateService();
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello"));

        var result = await service.UploadAsync(
            new ObjectStorageUploadRequest(
                "../outside.txt",
                "text/plain",
                content.Length,
                content));

        result.Success.Should().BeFalse();
        File.Exists(Path.Combine(_tempDirectory, "..", "outside.txt")).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private LocalFileSystemObjectStorageService CreateService()
        => new(
            Options.Create(new ObjectStorageSettings
            {
                Provider = "local",
                LocalBasePath = _tempDirectory,
                LocalBaseUrl = "http://localhost/files"
            }),
            NullLogger<LocalFileSystemObjectStorageService>.Instance);
}
