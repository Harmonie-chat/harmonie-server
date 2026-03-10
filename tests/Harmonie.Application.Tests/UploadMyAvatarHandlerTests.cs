using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.UploadMyAvatar;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class UploadMyAvatarHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UploadMyAvatarHandler _handler;

    public UploadMyAvatarHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _objectStorageServiceMock
            .Setup(x => x.BuildPublicUrl(It.IsAny<string>()))
            .Returns<string>(storageKey => $"https://files.test/{storageKey}");

        _handler = new UploadMyAvatarHandler(
            _userRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            _unitOfWorkMock.Object,
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

        var response = await _handler.HandleAsync(
            "avatar.png",
            "image/png",
            stream,
            userId);

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
        var user = CreateUser();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectStorageUploadResult.Failed("Object storage upload failed."));

        using var stream = CreateTestImageStream();

        var response = await _handler.HandleAsync(
            "avatar.png",
            "image/png",
            stream,
            user.Id);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.StorageUnavailable);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldUploadResizeAndUpdateAvatarUrl()
    {
        var user = CreateUser();
        ObjectStorageUploadRequest? capturedUploadRequest = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ObjectStorageUploadRequest, CancellationToken>((req, _) => capturedUploadRequest = req)
            .ReturnsAsync(ObjectStorageUploadResult.Succeeded());

        using var stream = CreateTestImageStream(512, 512);

        var response = await _handler.HandleAsync(
            "avatar.png",
            "image/png",
            stream,
            user.Id);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AvatarUrl.Should().StartWith("https://files.test/avatars/");

        capturedUploadRequest.Should().NotBeNull();
        capturedUploadRequest!.StorageKey.Should().StartWith($"avatars/{user.Id}/");
        capturedUploadRequest.ContentType.Should().Be("image/png");

        _userRepositoryMock.Verify(
            x => x.UpdateProfileAsync(
                It.Is<ProfileUpdateParameters>(p =>
                    p.AvatarUrlIsSet == true &&
                    p.AvatarUrl != null && p.AvatarUrl.StartsWith("https://files.test/avatars/") &&
                    p.DisplayNameIsSet == false &&
                    p.BioIsSet == false),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_ShouldDeleteUploadedObject()
    {
        var user = CreateUser();
        string? uploadedStorageKey = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _objectStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<ObjectStorageUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ObjectStorageUploadRequest, CancellationToken>((request, _) => uploadedStorageKey = request.StorageKey)
            .ReturnsAsync(ObjectStorageUploadResult.Succeeded());

        _userRepositoryMock
            .Setup(x => x.UpdateProfileAsync(
                It.IsAny<ProfileUpdateParameters>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        using var stream = CreateTestImageStream();

        var action = async () => await _handler.HandleAsync(
            "avatar.png",
            "image/png",
            stream,
            user.Id);

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

    private static User CreateUser()
    {
        var emailResult = Email.Create($"test-{Guid.NewGuid():N}@harmonie.chat");
        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Failed to create email for tests.");

        var usernameResult = Username.Create($"user{Guid.NewGuid():N}"[..20]);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create username for tests.");

        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            "hashed_password");
        if (userResult.IsFailure || userResult.Value is null)
            throw new InvalidOperationException("Failed to create user for tests.");

        return userResult.Value;
    }
}
