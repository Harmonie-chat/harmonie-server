using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Uploads.DeleteFile;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Uploads;

public sealed class DeleteFileHandlerTests
{
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly DeleteFileHandler _handler;

    public DeleteFileHandlerTests()
    {
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new DeleteFileHandler(
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<DeleteFileHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenFileDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();
        var fileId = UploadedFileId.New();

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Uploads.UploadedFile?)null);

        var response = await _handler.HandleAsync(new DeleteFileInput(fileId), userId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFileUploadedByAnotherUser_ShouldReturnAccessDenied()
    {
        var requestingUser = UserId.New();
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: UserId.New());

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(uploadedFile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        var response = await _handler.HandleAsync(new DeleteFileInput(uploadedFile.Id), requestingUser);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldDeleteFromRepositoryAndStorage()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: user.Id);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(uploadedFile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new DeleteFileInput(uploadedFile.Id), user.Id);

        response.Success.Should().BeTrue();
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(uploadedFile.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeleteFails_ShouldStillReturnSuccessAndLogWarning()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: user.Id);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(uploadedFile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        var response = await _handler.HandleAsync(new DeleteFileInput(uploadedFile.Id), user.Id);

        response.Success.Should().BeTrue();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
