using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Uploads.DownloadFile;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Uploads;

public sealed class DownloadFileHandlerTests
{
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly DownloadFileHandler _handler;

    public DownloadFileHandlerTests()
    {
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();

        _handler = new DownloadFileHandler(
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            NullLogger<DownloadFileHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenFileDoesNotExist_ShouldReturnNotFound()
    {
        var fileId = UploadedFileId.New();
        var userId = UserId.New();

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadedFile?)null);

        var response = await _handler.HandleAsync(fileId, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);
        _objectStorageServiceMock.Verify(
            x => x.GetStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageStreamIsNull_ShouldReturnStorageUnavailable()
    {
        var userId = UserId.New();
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: userId, fileName: "test.txt", contentType: "text/plain", sizeBytes: 42, storageKey: "uploads/2026/03/abc123.txt");

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(uploadedFile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.GetStreamAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var response = await _handler.HandleAsync(uploadedFile.Id, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnFileStream()
    {
        var userId = UserId.New();
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: userId, fileName: "test.txt", contentType: "text/plain", sizeBytes: 42, storageKey: "uploads/2026/03/abc123.txt");
        var fileStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("file content"));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(uploadedFile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.GetStreamAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        var response = await _handler.HandleAsync(uploadedFile.Id, userId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ContentType.Should().Be("text/plain");
        response.Data.FileName.Should().Be("test.txt");
        response.Data.Content.Should().BeSameAs(fileStream);
    }

}
