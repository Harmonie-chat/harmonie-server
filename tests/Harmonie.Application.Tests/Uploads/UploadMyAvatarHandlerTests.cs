using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Users.UploadMyAvatar;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Harmonie.Application.Tests.Uploads;

public sealed class UploadMyAvatarHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UploadMyAvatarHandler _handler;

    public UploadMyAvatarHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _userRepositoryMock
            .Setup(x => x.GetUserNotificationContextAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserNotificationContext(Array.Empty<GuildId>(), Array.Empty<ConversationId>()));

        _handler = new UploadMyAvatarHandler(
            _userRepositoryMock.Object,
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
            _unitOfWorkMock.Object,
            Mock.Of<IUserProfileNotifier>(),
            NullLogger<UploadMyAvatarHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        using var stream = CreateTestImageStream();
        var input = new UploadMyAvatarInput("avatar.png", "image/png", stream);

        var response = await _handler.HandleAsync(input, userId, TestContext.Current.CancellationToken);

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

        using var stream = CreateTestImageStream();
        var input = new UploadMyAvatarInput("avatar.png", "image/png", stream);

        var response = await _handler.HandleAsync(input, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldUploadResizeAndUpdateAvatarFile()
    {
        var user = ApplicationTestBuilders.CreateUser();
        ObjectStorageUploadRequest? capturedUploadRequest = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ObjectStorageUploadRequest, CancellationToken>((req, _) => capturedUploadRequest = req)
            .ReturnsAsync(ObjectStorageUploadResult.Succeeded());

        using var stream = CreateTestImageStream(512, 512);
        var input = new UploadMyAvatarInput("avatar.png", "image/png", stream);

        var response = await _handler.HandleAsync(input, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AvatarFileId.Should().NotBeEmpty();

        capturedUploadRequest.Should().NotBeNull();
        capturedUploadRequest!.StorageKey.Should().StartWith($"avatars/{user.Id}/");
        capturedUploadRequest.ContentType.Should().Be("image/png");

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

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

        _userRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        using var stream = CreateTestImageStream();
        var input = new UploadMyAvatarInput("avatar.png", "image/png", stream);

        var action = async () => await _handler.HandleAsync(input, user.Id, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();

        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(uploadedStorageKey!, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MemoryStream CreateTestImageStream(int width = 100, int height = 100)
    {
        using var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }

}
