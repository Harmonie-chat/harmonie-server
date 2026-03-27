using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Uploads;

public sealed class UploadFileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UploadFileHandler _handler;

    public UploadFileHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new UploadFileHandler(
            _userRepositoryMock.Object,
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<UploadFileHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        using var stream = CreateStream("hello");
        var input = new UploadFileInput("hello.txt", "text/plain", stream.Length, stream, UploadPurpose.Attachment);

        var response = await _handler.HandleAsync(input, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
        _objectStorageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenObjectStorageUploadFails_ShouldReturnServiceUnavailable()
    {
        var user = ApplicationTestBuilders.CreateUser();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectStorageUploadResult.Failed("Object storage upload failed."));

        using var stream = CreateStream("hello");
        var input = new UploadFileInput("hello.txt", "text/plain", stream.Length, stream, UploadPurpose.Attachment);

        var response = await _handler.HandleAsync(input, user.Id);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.StorageUnavailable);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldUploadPersistAndCommit()
    {
        var user = ApplicationTestBuilders.CreateUser();
        UploadedFile? persistedFile = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectStorageUploadResult.Succeeded());

        _uploadedFileRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UploadedFile>(), It.IsAny<CancellationToken>()))
            .Callback<UploadedFile, CancellationToken>((uploadedFile, _) => persistedFile = uploadedFile)
            .Returns(Task.CompletedTask);

        using var stream = CreateStream("hello");
        var input = new UploadFileInput("hello.txt", "text/plain", stream.Length, stream, UploadPurpose.Attachment);

        var response = await _handler.HandleAsync(input, user.Id);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.FileId.Should().NotBeEmpty();
        response.Data!.Filename.Should().Be("hello.txt");
        response.Data.ContentType.Should().Be("text/plain");
        response.Data.SizeBytes.Should().Be(stream.Length);
        persistedFile.Should().NotBeNull();
        persistedFile!.UploaderUserId.Should().Be(user.Id);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_ShouldDeleteUploadedObject()
    {
        var user = ApplicationTestBuilders.CreateUser();
        string? uploadedStorageKey = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ObjectStorageUploadRequest, CancellationToken>((request, _) => uploadedStorageKey = request.StorageKey)
            .ReturnsAsync(ObjectStorageUploadResult.Succeeded());

        _uploadedFileRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UploadedFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        using var stream = CreateStream("hello");
        var input = new UploadFileInput("hello.txt", "text/plain", stream.Length, stream, UploadPurpose.Attachment);

        var action = async () => await _handler.HandleAsync(input, user.Id);

        await action.Should().ThrowAsync<InvalidOperationException>();

        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(uploadedStorageKey!, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MemoryStream CreateStream(string content)
        => new(System.Text.Encoding.UTF8.GetBytes(content));

}
