using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Users.DeleteMyAvatar;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Uploads;

public sealed class DeleteMyAvatarHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly DeleteMyAvatarHandler _handler;

    public DeleteMyAvatarHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _handler = new DeleteMyAvatarHandler(
            _userRepositoryMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
            _unitOfWorkMock.Object,
            NullLogger<DeleteMyAvatarHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await _handler.HandleAsync(userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);

        _userRepositoryMock.Verify(
            x => x.UpdateProfileAsync(It.IsAny<ProfileUpdateParameters>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAvatarIsNotSet_ShouldReturnNotFound()
    {
        var user = ApplicationTestBuilders.CreateUser();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(user.Id);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);

        _unitOfWorkMock.Verify(
            x => x.BeginAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAvatarExists_ShouldClearAvatarAfterCommitAndCleanupStoredFile()
    {
        var avatarFileId = UploadedFileId.From(Guid.Parse("7d839916-c19a-45db-a0e2-cf7ea8ad31fb"));
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateAvatarFile(avatarFileId);
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(id: avatarFileId, uploaderUserId: user.Id, fileName: "avatar-old.png", contentType: "image/png", sizeBytes: 123, storageKey: "avatars/old-avatar.png", purpose: UploadPurpose.Avatar);
        var sequence = new MockSequence();

        _userRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _userRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.UpdateProfileAsync(
                It.Is<ProfileUpdateParameters>(p =>
                    p.UserId == user.Id &&
                    p.AvatarFileIdIsSet &&
                    p.AvatarFileId == null &&
                    p.DisplayNameIsSet == false &&
                    p.BioIsSet == false),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .InSequence(sequence)
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(avatarFileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .InSequence(sequence)
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.DeleteAsync(avatarFileId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(user.Id);

        response.Success.Should().BeTrue();
        user.AvatarFileId.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(avatarFileId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
